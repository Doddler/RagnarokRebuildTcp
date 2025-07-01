using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using System.Diagnostics;
using RoRebuildServer.Data;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers;

public abstract class SkillHandlerTrap : SkillHandlerBase
{
    protected abstract string GroundUnitType();
    protected abstract CharacterSkill SkillType();
    protected virtual int Catalyst() => -1;
    protected virtual int CatalystCount() => 1;

    public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position,
        int lvl, bool isIndirect)
    {
        if (source.Character.Type == CharacterType.Player)
        {
            var item = Catalyst();
            if (item > 0)
            {
                var count = CatalystCount();
                if (source.Character.Type == CharacterType.Player && (source.Player.Inventory == null || source.Player.Inventory.GetItemCount(item) < count))
                    return SkillValidationResult.MissingRequiredItem;
            }
        }

        return base.ValidateTarget(source, target, position, lvl, false);
    }

    //pre-validation occurs after the cast bar and is the last chance for a skill to fail.
    //Default validation will make sure we have LoS and the cell is valid
    public override bool PreProcessValidation(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        var map = source.Character.Map;
        Debug.Assert(map != null);

        //we check the cell here because it could have changed since regular validation via ice wall, script, etc.
        if (!map.WalkData.IsCellWalkable(position))
        {
            if (source.Character.Type == CharacterType.Player)
                CommandBuilder.SkillFailed(source.Player, SkillValidationResult.Failure);
            return false;
        }

        var distance = 1;
        if (source.Character.Type == CharacterType.Monster)
            distance = 0;

        var effectiveArea = Area.CreateAroundPoint(position, distance);
        if (map.DoesAreaOverlapWithTrapsOrCharacters(effectiveArea))
        {
            if (source.Character.Type == CharacterType.Player)
                CommandBuilder.SkillFailed(source.Player, SkillValidationResult.TrapTooClose);
            return false;
        }

        return true;
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        var map = source.Character.Map;
        Debug.Assert(map != null);

        if (source.Character.Type == CharacterType.Player && !source.Player.TryRemoveItemFromInventory(Catalyst(), CatalystCount(), true))
            return;

        var ch = source.Character;

        var e = World.Instance.CreateEvent(source.Entity, map, GroundUnitType(), position, lvl, 0, 0, 0, null);
        ch.AttachEvent(e);
        source.ApplyCooldownForSupportSkillAction();

        if (!isIndirect)
            CommandBuilder.SkillExecuteAreaTargetedSkillAutoVis(ch, position, SkillType(), lvl);
    }
}

public abstract class TrapBaseEvent : NpcBehaviorBase
{
    protected abstract CharacterSkill SkillSource();
    protected abstract NpcEffectType EffectType();
    protected abstract float Duration(int skillLevel);

    public override void InitEvent(Npc npc, int param1, int param2, int param3, int param4, string? paramString)
    {
        npc.RevealAsEffect(EffectType(), "Trap");
        npc.ValuesInt[0] = param1;
        npc.ValuesInt[2] = 0;

        if (!npc.Owner.TryGet<WorldObject>(out var owner))
        {
            ServerLogger.LogWarning($"Npc {npc.Character} running trap init but does not have an owner.");
            return;
        }

        var targeting = new TargetingInfo()
        {
            Faction = owner.Type == CharacterType.Player ? 1 : 0,
            Party = 0,
            IsPvp = false,
            SourceEntity = npc.Owner,
            TargetingType = TargetingType.Enemies
        };

        var aoe = World.Instance.GetNewAreaOfEffect();
        aoe.Init(npc.Character, Area.CreateAroundPoint(npc.Character.Position, 1), AoeType.SpecialEffect, targeting, Duration(npc.ValuesInt[0]), 0.25f, 0, 0);
        aoe.TriggerOnFirstTouch = true;
        aoe.CheckStayTouching = false; //if they die or something in the aoe and lose their status we want to give it back promptly
        aoe.Class = AoEClass.Trap;
        aoe.SkillSource = SkillSource();

        npc.AreaOfEffect = aoe;
        npc.Character.Map!.CreateAreaOfEffect(aoe);
        npc.StartTimer(50);
    }

    public override void OnTimer(Npc npc, float lastTime, float newTime)
    {
        if (newTime > Duration(npc.ValuesInt[0]))
        {
            OnNaturalExpiration(npc);
            npc.EndEvent();
        }

        if(npc.ValuesInt[2] > 0)
            npc.EndEvent();
    }

    public virtual void OnNaturalExpiration(Npc npc) {}

    public abstract bool TriggerTrap(Npc npc, CombatEntity src, CombatEntity target, int skillLevel);

    public override void OnAoEInteraction(Npc npc, CombatEntity target, AreaOfEffect aoe)
    {
        if (!npc.Owner.TryGet<CombatEntity>(out var owner) || owner.Character.Map != npc.Character.Map)
        {
            npc.ValuesInt[2] = 1;
        }
        else
        {
            if (TriggerTrap(npc, owner, target, npc.ValuesInt[0]))
                npc.ValuesInt[2] = 1;
        }
    }

    private static int FlyingTag = -1;

    protected bool IsFlying(CombatEntity target)
    {
        if (target.Character.Type != CharacterType.Monster)
            return false;

        if (FlyingTag == -1)
        {
            if (!DataManager.TagToIdLookup.TryGetValue("Flying", out FlyingTag))
                return false;
        }

        var mb = target.Character.Monster.MonsterBase;
        if (mb.Code == "CLOCK")
            return false; //I really should do something better for this, but I want them trappable but still affected by cards that work against flying

        return target.Character.Monster.MonsterBase.Tags?.Contains(FlyingTag) ?? false;
    }
}
