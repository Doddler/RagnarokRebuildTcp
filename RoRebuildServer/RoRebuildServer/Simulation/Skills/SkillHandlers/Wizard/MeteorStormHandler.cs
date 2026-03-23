using Antlr4.Runtime.Atn;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Skills.SkillHandlers.Hunter;
using RoRebuildServer.Simulation.Util;
using System.Diagnostics;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Wizard;

[SkillHandler(CharacterSkill.MeteorStorm, SkillClass.Magic, SkillTarget.Ground)]
public class MeteorStormHandler : SkillHandlerBase
{
    public override int GetAreaOfEffect(CombatEntity source, Position position, int lvl) => lvl <= 10 ? 6 : 9;

    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl) => 10f;

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        var map = source.Character.Map;
        Debug.Assert(map != null);

        var ch = source.Character;

        if (!position.IsValid())
            position = target != null ? target.Character.Position : source.Character.Position;

        var meteorCount = (4 + lvl) / 2;
        if (lvl > 10)
            meteorCount = 10;

        var maxDistance = 3;
        if(lvl > 10)
            maxDistance = 11;

        var dropArea = Area.CreateAroundPoint(position, maxDistance);

        for (var i = 0; i < meteorCount; i++)
        {
            var randomPos = dropArea.RandomInArea();

            if (!map.IsWalkableTile(randomPos))
                continue;
            
            var e = World.Instance.CreateEvent(source.Entity, map, "MeteorStormObjectEvent", randomPos, i, lvl, 0, 0, null);
            ch.AttachEvent(e);
        }

        source.ApplyCooldownForSupportSkillAction();

        if (!isIndirect)
            CommandBuilder.SkillExecuteAreaTargetedSkillAutoVis(ch, position, CharacterSkill.MeteorStorm, lvl);
    }
}

public class MeteorStormObjectEvent : NpcBehaviorBase
{
    private static readonly string[] MeteorTypes = ["Meteor1", "Meteor2", "Meteor3", "Meteor4"];

    public override void InitEvent(Npc npc, int param1, int param2, int param3, int param4, string? paramString)
    {
        Debug.Assert(npc.Character.Map != null);
        npc.ValuesInt[0] = param1; //sequenceId
        npc.ValuesInt[1] = param2; //skillLevel
        npc.StartTimer(50);

        if (param1 == 0)
            RevealMeteor(npc);
    }

    private void RevealMeteor(Npc npc)
    {
        using var targetList = EntityListPool.Get();

        npc.Character.Map?.GatherPlayersInRange(npc.SelfPosition, ServerConfig.MaxViewDistance + 2, targetList, false, false);
        CommandBuilder.AddRecipients(targetList);
        var id = DataManager.EffectIdForName[MeteorTypes[GameRandom.Next(0, 4)]];
        CommandBuilder.SendEffectAtLocationMulti(id, npc.SelfPosition, 0);
        CommandBuilder.ClearRecipients();
    }

    public override void OnTimer(Npc npc, float lastTime, float newTime)
    {
        var seq = npc.ValuesInt[0];
        var impact = seq + 0.7f;

        if (seq > 0 && lastTime < seq && newTime >= seq)
            RevealMeteor(npc);

        if (lastTime < impact && newTime >= impact)
        {
            using var targetList = EntityListPool.Get();

            if (!npc.Owner.TryGet<CombatEntity>(out var owner))
            {
                npc.EndEvent();
                return;
            }

            var skillLevel = npc.ValuesInt[1];
            var count = (skillLevel + 1) / 2;

            npc.Character.Map?.GatherTargetableEntitiesInRange(npc.Character.Position, 3, targetList, true);
            foreach (var e in targetList)
            {
                if (!e.TryGet<CombatEntity>(out var ce) || ce.Character.Map == null)
                    continue;

                if (!ce.IsValidTarget(owner, false, false))
                    continue;

                var res = owner.CalculateCombatResult(ce, count, 1, AttackFlags.Magical, CharacterSkill.MeteorStorm, AttackElement.Fire);
                res.IsIndirect = true;
                res.Damage /= count; //damage is calculated as a single hit, but split across hit count hits
                res.HitCount = (byte)count;
                res.Time = Time.ElapsedTimeFloat;

                if (res.IsDamageResult)
                    owner.TryStunTarget(ce, int.Min(300, skillLevel * 30));

                CommandBuilder.SkillExecuteIndirectAutoVisibility(npc.Character, ce.Character, res);
                owner.ExecuteCombatResult(res, false);
            }

            npc.EndEvent();
        }
    }
}

public class NpcLoaderMeteorStormEvent : INpcLoader
{
    public void Load()
    {
        DataManager.RegisterEvent("MeteorStormObjectEvent", new MeteorStormObjectEvent());
    }
}