using System.Diagnostics;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Acolyte;

[MonsterSkillHandler(CharacterSkill.Pneuma, SkillClass.Magic, SkillTarget.Ally)]
[SkillHandler(CharacterSkill.Pneuma, SkillClass.Magic, SkillTarget.Ground)]
public class PneumaHandler : SkillHandlerBase
{
    public override bool PreProcessValidation(CombatEntity source, CombatEntity? target, Position position, int lvl,
        bool isIndirect, bool isItemSource)
    {
        var map = source.Character.Map;
        Debug.Assert(map != null);

        if (!position.IsValid() && source.Character.Type == CharacterType.Monster) //monsters will either target pneuma directly on themselves or an ally
            position = target != null ? target.Character.Position : source.Character.Position;

        var effectiveArea = Area.CreateAroundPoint(position, 1);
        if (map.HasAreaOfEffectTypeInArea(effectiveArea, CharacterSkill.Pneuma, CharacterSkill.SafetyWall))
        {
            if (source.Character.Type == CharacterType.Player)
                CommandBuilder.SkillFailed(source.Player, SkillValidationResult.OverlappingAreaOfEffect);
            return false;
        }

        return true;
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        var map = source.Character.Map;
        Debug.Assert(map != null);

        if (!position.IsValid()) //monsters will either target pneuma directly on themselves or an ally
            position = target != null ? target.Character.Position : source.Character.Position;

        var ch = source.Character;

        var e = World.Instance.CreateEvent(source.Entity, map, "PneumaObjectEvent", position, lvl, 0, 0, 0, null);
        ch.AttachEvent(e);
        //source.ApplyCooldownForSupportSkillAction();

        if (!isIndirect)
            CommandBuilder.SkillExecuteAreaTargetedSkillAutoVis(ch, position, CharacterSkill.Pneuma, lvl);
    }
}

public class PneumaObjectEvent : NpcBehaviorBase
{
    public override void InitEvent(Npc npc, int param1, int param2, int param3, int param4, string? paramString)
    {
        npc.RevealAsEffect(NpcEffectType.Pneuma, "Pneuma");

        if (!npc.Owner.TryGet<WorldObject>(out var owner))
        {
            ServerLogger.LogWarning($"Npc {npc.Character} running PneumaObjectEvent init but does not have an owner.");
            return;
        }

        var targeting = new TargetingInfo()
        {
            Faction = owner.Type == CharacterType.Player ? 1 : 0,
            Party = 0,
            IsPvp = false,
            SourceEntity = npc.Owner,
            TargetingType = TargetingType.Everyone
        };

        var aoe = World.Instance.GetNewAreaOfEffect();
        aoe.Init(npc.Character, Area.CreateAroundPoint(npc.Character.Position, 1), AoeType.SpecialEffect, targeting, 10f, 0.25f, 0, 0);
        aoe.TriggerOnFirstTouch = true;
        aoe.CheckStayTouching = true; //if they die or something in the aoe and lose their status we want to give it back promptly
        aoe.SkillSource = CharacterSkill.Pneuma;

        npc.AreaOfEffect = aoe;
        npc.Character.Map!.CreateAreaOfEffect(aoe);
        npc.StartTimer(1000);
    }

    public override void OnTimer(Npc npc, float lastTime, float newTime)
    {
        if (newTime > 10f)
            npc.EndEvent();
    }

    public override void OnAoEInteraction(Npc npc, CombatEntity target, AreaOfEffect aoe)
    {
        if (target.HasStatusEffectOfType(CharacterStatusEffect.Pneuma))
            return;

        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.Pneuma, (float)(aoe.Expiration - Time.ElapsedTime));
        target.AddStatusEffect(status);
    }
}

public class NpcLoaderPneumaEvents : INpcLoader
{
    public void Load()
    {
        DataManager.RegisterEvent("PneumaObjectEvent", new PneumaObjectEvent());
    }
}