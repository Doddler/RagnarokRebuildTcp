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
using System.Diagnostics;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Mage;

[MonsterSkillHandler(CharacterSkill.SafetyWall, SkillClass.Magic, SkillTarget.Ally)]
[SkillHandler(CharacterSkill.SafetyWall, SkillClass.Magic, SkillTarget.Ground, SkillPreferredTarget.Self)]
public class SafetyWallHandler : SkillHandlerBase
{
    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl) => float.Clamp(4.5f - lvl * 0.5f, 1f, 5f);

    private const int GemstoneId = 717;

    //we use this instead of ValidateTarget because we only want the cast to fail at the end of the cast time if the target cell is overlapping
    public override bool PreProcessValidation(CombatEntity source, CombatEntity? target, Position position, int lvl,
        bool isIndirect, bool isItemSource)
    {
        var map = source.Character.Map;
        Debug.Assert(map != null);

        if (!position.IsValid() && source.Character.Type == CharacterType.Monster) //monsters will either target pneuma directly on themselves or an ally
            position = target != null ? target.Character.Position : source.Character.Position;

        var effectiveArea = Area.CreateAroundPoint(position, 0);
        if (map.HasAreaOfEffectTypeInArea(effectiveArea, CharacterSkill.Pneuma, CharacterSkill.SafetyWall))
        {
            if(source.Character.Type == CharacterType.Player)
                CommandBuilder.SkillFailed(source.Player, SkillValidationResult.OverlappingAreaOfEffect);
            return false;
        }

        return true;
    }

    public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position,
        int lvl, bool isIndirect, bool isItemSource)
    {
        if (!isIndirect && !CheckRequiredGemstone(source, GemstoneId, false))
            return SkillValidationResult.MissingRequiredItem;

        return base.ValidateTarget(source, target, position, lvl, false, false);
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        var map = source.Character.Map;
        Debug.Assert(map != null);

        if (!isIndirect && !ConsumeGemstoneForSkillWithFailMessage(source, BlueGemstone))
            return;

        if (!position.IsValid() && source.Character.Type == CharacterType.Monster) //monsters will either target pneuma directly on themselves or an ally
            position = target != null ? target.Character.Position : source.Character.Position;

        var ch = source.Character;

        var e = World.Instance.CreateEvent(source.Entity, map, "SafetyWallObjectEvent", position, lvl, 0, 0, 0, null);
        ch.AttachEvent(e);
        source.ApplyCooldownForSupportSkillAction();

        if (!isIndirect)
            CommandBuilder.SkillExecuteAreaTargetedSkillAutoVis(ch, position, CharacterSkill.SafetyWall, lvl);
    }
}

public class SafetyWallObjectEvent : NpcBehaviorBase
{
    public override void InitEvent(Npc npc, int param1, int param2, int param3, int param4, string? paramString)
    {
        npc.RevealAsEffect(NpcEffectType.SafetyWall, "SafetyWall");

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

        npc.ValuesInt[0] = param1 * 5;

        var aoe = World.Instance.GetNewAreaOfEffect();
        aoe.Init(npc.Character, Area.CreateAroundPoint(npc.Character.Position, 0), AoeType.SpecialEffect, targeting, npc.ValuesInt[0], 0.25f, param1 + 2, 0);
        aoe.TriggerOnFirstTouch = true;
        aoe.CheckStayTouching = true;
        aoe.SkillSource = CharacterSkill.SafetyWall;
        
        npc.AreaOfEffect = aoe;
        npc.Character.Map!.CreateAreaOfEffect(aoe);
        npc.StartTimer(100);
    }

    public override void OnTimer(Npc npc, float lastTime, float newTime)
    {
        if (npc.ValuesInt[0] < newTime || npc.AreaOfEffect == null || npc.AreaOfEffect.Value1 <= 0)
            npc.EndEvent();
    }

    //the Safety Wall status exits only to tell the combat handler it should look to see if a safety wall exists on the character's tile.
    //We don't remove the status if the wall expires early, but the combat handler will simply not see the safety wall and ignore it.
    public override void OnAoEInteraction(Npc npc, CombatEntity target, AreaOfEffect aoe)
    {
        if (target.HasStatusEffectOfType(CharacterStatusEffect.SafetyWall))
            return;

        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.SafetyWall, (float)(aoe.Expiration - Time.ElapsedTime));
        target.AddStatusEffect(status);
    }

    //this is triggered any time a player in the safety wall takes damage so we can end early if we take enough hits
    public override void OnAoEEvent(Npc npc, CombatEntity target, AreaOfEffect aoe, object? eventData)
    {
        aoe.Value1--;
        if (aoe.Value1 <= 0)
            npc.EndEvent();
    }
}

public class NpcLoaderSafetyWallEvents : INpcLoader
{
    public void Load()
    {
        DataManager.RegisterEvent("SafetyWallObjectEvent", new SafetyWallObjectEvent());
    }
}
