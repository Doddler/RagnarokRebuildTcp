using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;
using System.Diagnostics;
using System.Numerics;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Wizard;

[SkillHandler(CharacterSkill.StormGust, SkillClass.Magic, SkillTarget.Ground)]
public class StormGustHandler : SkillHandlerBase
{
    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl) => 5 + 1 * lvl;

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        Debug.Assert(source.Character.Map != null);
        using var targetList = EntityListPool.Get();

        var ch = source.Character;

        var e = World.Instance.CreateEvent(source.Entity, source.Character.Map, "StormGustObjectEvent", position, lvl, 0, 0, 0, null);
        ch.AttachEvent(e);

        if (!isIndirect)
        {
            CommandBuilder.SkillExecuteAreaTargetedSkillAutoVis(ch, position, CharacterSkill.StormGust, lvl);
            source.ApplyCooldownForSupportSkillAction();
            source.ApplyAfterCastDelay(4f);
        }
    }
}

public class StormGustObjectEvent : NpcBehaviorBase
{
    public override void InitEvent(Npc npc, int param1, int param2, int param3, int param4, string? paramString)
    {
        Debug.Assert(npc.Character.Map != null);
        npc.ValuesInt[0] = param1;
        npc.StartTimer(50);

        var position = npc.SelfPosition;
        using var targetList = EntityListPool.Get();

        //send the thunder aoe
        npc.Character.Map.GatherPlayersInRange(position, ServerConfig.MaxViewDistance + 4, targetList, false, false);
        CommandBuilder.AddRecipients(targetList);
        var id = DataManager.EffectIdForName["StormGust"];
        CommandBuilder.SendEffectAtLocationMulti(id, npc.SelfPosition, 0);
        CommandBuilder.ClearRecipients();

        if (!npc.Owner.TryGet<CombatEntity>(out var owner))
        {
            ServerLogger.LogWarning($"Failed to init StormGust object event as it has no owner or source entity!");
            return;
        }

        var targeting = new TargetingInfo()
        {
            Faction = owner.Character.Type == CharacterType.Player ? 1 : 0,
            Party = 0,
            IsPvp = false,
            SourceEntity = npc.Owner,
            TargetingType = TargetingType.Enemies
        };

        var size = npc.ValuesInt[0] <= 10 ? 5 : 7;

        var aoe = World.Instance.GetNewAreaOfEffect();
        aoe.Init(npc.Character, Area.CreateAroundPoint(npc.Character.Position, size), AoeType.DamageAoE, targeting, 5f, 0.1f, 0, 0);
        aoe.CheckStayTouching = true;
        aoe.SkillSource = CharacterSkill.StormGust;

        npc.AreaOfEffect = aoe;
        npc.Character.Map!.CreateAreaOfEffect(aoe);
    }


    public override void OnTimer(Npc npc, float lastTime, float newTime)
    {
        if (!npc.Owner.TryGet<CombatEntity>(out _))
        {
            npc.EndEvent();
            return;
        }

        if (newTime > 4.6f)
            npc.EndEvent();
    }


    public override void OnAoEInteraction(Npc npc, CombatEntity target, AreaOfEffect aoe)
    {
        if (!aoe.TargetingInfo.SourceEntity.TryGet<CombatEntity>(out var src))
            return;

        if (src.Character.Map != npc.Character.Map)
            return;

        if (!target.IsValidTarget(src) || target.IsInSkillDamageCooldown(CharacterSkill.StormGust))
            return;

        var ratio = 1f + 0.3f * int.Clamp(npc.ValuesInt[0], 1, 10);

        var res = src.CalculateCombatResult(target, ratio, 1, AttackFlags.Magical, CharacterSkill.StormGust, AttackElement.Water);
        res.SetTimingInstant();
        res.IsIndirect = true;

        if (res.IsDamageResult)
        {
            var status = target.AddOrStackStatusEffect(CharacterStatusEffect.StormGustHitCounter, 60, 3);
            var applyHitCooldown = true;
            if (status.Value4 >= 3)
            {
                target.RemoveStatusOfTypeIfExists(CharacterStatusEffect.StormGustHitCounter);
                if (src.TryFreezeTarget(target, 2000, 0.1f))
                    applyHitCooldown = false;
            }

            res.KnockBack = 2;
            res.AttackPosition = npc.SelfPosition;

            if(applyHitCooldown)
                target.SetSkillDamageCooldown(CharacterSkill.StormGust, 0.45f);
        }

        CommandBuilder.SkillExecuteIndirectAutoVisibility(npc.Character, target.Character, res);
        src.ExecuteCombatResult(res, false);
    }
}

public class NpcLoaderStormGustEvent : INpcLoader
{
    public void Load()
    {
        DataManager.RegisterEvent("StormGustObjectEvent", new StormGustObjectEvent());
    }
}