using System.Diagnostics;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Simulation.Util;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using RoRebuildServer.Simulation.Pathfinding;
using RoRebuildServer.Networking;
using System.Numerics;

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

    public void AddExp(Player player, int baseExp, int jobExp)
    {
        var party = player.Party;
        if (party != null)
            party.OnlineMembers.ClearInactive();
        if (party == null || party.OnlineMembers.Count == 1)
        {
            player.GainBaseExp(baseExp);
            player.GainJobExp(jobExp);
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
                if (p.Character.Map != player.Character.Map)
                    continue;
                //if (p.Character.Position.DistanceTo(player.Character.Position) > 60)
                //    continue;
                var difference = Math.Abs(player.CharacterLevel - p.CharacterLevel);
                if (difference > 15)
                    continue;
                if (difference > 10)
                {
                    var rate = 100 / (difference - 9);
                    baseExp = baseExp * rate / 100;
                    jobExp = jobExp * rate / 100;
                    if (baseExp == 0 && jobExp == 0)
                        continue;
                }
                Results.Add(p.Entity, new ExpResult(p, baseExp, jobExp));
            }
            else
            {
                if (Math.Abs(player.CharacterLevel - existing.Player.CharacterLevel) > 10)
                    continue;
                existing.BaseExp += baseExp;
                existing.JobExp += jobExp;
                existing.ContributingPlayers++;
            }
        }
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

    public void DistributeExp()
    {
        CountPlayersInEachParty();

        foreach (var (_, result) in Results)
        {
            if (result.Player.Party == null)
                continue; //should never happen

            var earningPlayers = PartySplitCounts[result.Player.Party.PartyId];
            var nonContributors = earningPlayers - result.ContributingPlayers;
            var rate = 1f - 0.055f * nonContributors; //-5.5% per player that gets exp but didn't contribute to the kill

            var baseExp = (int)float.Ceiling(result.BaseExp * rate);
            var jobExp = (int)float.Ceiling(result.JobExp * rate);

            result.Player.GainBaseExp(baseExp);
            result.Player.GainJobExp(jobExp);
            CommandBuilder.SendExpGain(result.Player, baseExp, jobExp);
        }

        Reset();
    }
}