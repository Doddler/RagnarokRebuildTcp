using System.Diagnostics;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Simulation.Util;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using RoRebuildServer.Simulation.Pathfinding;
using RoRebuildServer.Networking;
using System.Numerics;
using RebuildSharedData.Enum;
using RoRebuildServer.Data.Monster;

namespace RoRebuildServer.Simulation.Parties;

public class PartyExpAccumulator
{
    private struct ExpResult(Player player, int baseExp, int jobExp)
    {
        public Player Player = player;
        public int BaseExp = baseExp;
        public int JobExp = jobExp;
        public int ContributingPlayers = 1;
    }

    private Dictionary<Entity, ExpResult> Results = new();
    private Dictionary<int, int> PartySplitCounts = new();

    public void Reset()
    {
        Results.Clear();
        PartySplitCounts.Clear();
    }

    public void AddExp(Player player, MonsterDatabaseInfo src, int baseExp, int jobExp)
    {
        var party = player.Party;
        if (party != null)
            party.OnlineMembers.ClearInactive();
        if (party == null || party.OnlineMembers.Count == 1)
        {
            player.GainBaseExpFromMonster(baseExp, src);
            player.GainJobExpFromMonster(jobExp, src);
            CommandBuilder.SendExpGain(player, baseExp, jobExp);
            return;
        }

        foreach(var m in party.OnlineMembers)
        {
            ref var existing = ref CollectionsMarshal.GetValueRefOrNullRef(Results, m);
            if (Unsafe.IsNullRef(ref existing))
            {
                if (!m.TryGet<Player>(out var p))
                    continue;
                if (p.Character.Map != player.Character.Map || p.Character.State == CharacterState.Dead || !p.Character.IsActive)
                    continue;

                var (b, j) = GetLevelModifiedExp(player.CharacterLevel, p.CharacterLevel, baseExp, jobExp);
                if(b > 0 || j > 0)
                    Results.Add(p.Entity, new ExpResult(p, b, j));
            }
            else
            {
                var (b, j) = GetLevelModifiedExp(player.CharacterLevel, existing.Player.CharacterLevel, baseExp, jobExp);
                if (b <= 0 && j <= 0)
                    continue;
                existing.BaseExp += b;
                existing.JobExp += j;
                existing.ContributingPlayers++;
            }
        }
    }

    private (int, int) GetLevelModifiedExp(int srcLevel, int targetLevel, int baseExp, int jobExp)
    {
        var difference = Math.Abs(srcLevel - targetLevel);
        if (difference > 15)
            return (0, 0);
        if (difference <= 10)
            return (baseExp, jobExp);
        var rate = 100 / (difference - 9);
        baseExp = baseExp * rate / 100;
        jobExp = jobExp * rate / 100;

        return (baseExp, jobExp);
    }

    private void CountPlayersInEachParty()
    {
        foreach (var (_, result) in Results)
        {
            var p = result.Player.Party;
            if (p == null)
                continue;

            ref var existing = ref CollectionsMarshal.GetValueRefOrNullRef(PartySplitCounts, p.PartyId);
            if (Unsafe.IsNullRef(ref existing))
                PartySplitCounts[p.PartyId] = 1;
            else
                existing++;
        }
    }

    public void DistributeExp(MonsterDatabaseInfo src)
    {
        CountPlayersInEachParty();

        foreach (var (_, result) in Results)
        {
            if (result.Player.Party == null)
                continue; //should never happen

            var earningPlayers = PartySplitCounts[result.Player.Party.PartyId];
            var nonContributors = earningPlayers - result.ContributingPlayers;
            //var rate = 1f - 0.055f * nonContributors; //-5.5% per player that gets exp but didn't contribute to the kill
            var rate = MathF.Pow(0.92f, nonContributors); //-8% compounding per player

            var baseExp = (int)float.Ceiling(result.BaseExp * rate);
            var jobExp = (int)float.Ceiling(result.JobExp * rate);

            result.Player.GainBaseExpFromMonster(baseExp, src);
            result.Player.GainJobExpFromMonster(jobExp, src);
            CommandBuilder.SendExpGain(result.Player, baseExp, jobExp);
        }

        Reset();
    }
}