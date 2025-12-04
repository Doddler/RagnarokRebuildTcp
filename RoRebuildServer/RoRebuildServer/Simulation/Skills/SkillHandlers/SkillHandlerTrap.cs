using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using System.Diagnostics;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Simulation.Items;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers;

public abstract class SkillHandlerTrap : SkillHandlerBase
{
    protected abstract string GroundUnitType();
    protected abstract CharacterSkill SkillType();
    protected virtual int Catalyst() => -1;
    protected virtual int CatalystCount() => 1;

    public override SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position,
        int lvl, bool isIndirect, bool isItemSource)
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

        return base.ValidateTarget(source, target, position, lvl, false, false);
    }

    //pre-validation occurs after the cast bar and is the last chance for a skill to fail.
    //Default validation will make sure we have LoS and the cell is valid
    public override bool PreProcessValidation(CombatEntity source, CombatEntity? target, Position position, int lvl,
        bool isIndirect, bool isItemSource)
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

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        var map = source.Character.Map;
        Debug.Assert(map != null);

        if (source.Character.Type == CharacterType.Player && !source.Player.TryRemoveItemFromInventory(Catalyst(), CatalystCount(), true))
            return;

        var ch = source.Character;

        var e = World.Instance.CreateEvent(source.Entity, map, GroundUnitType(), position, lvl, 0, 0, 0, null, true);
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

    protected virtual bool Attackable => false;
    protected virtual bool AllowAutoAttackMove => false;
    protected virtual bool BlockMultipleActivations => true;
    protected virtual bool InheritOwnerFacing => false;

    public override void InitEvent(Npc npc, int param1, int param2, int param3, int param4, string? paramString)
    {
        if (npc.Character.Type != CharacterType.BattleNpc)
            throw new Exception($"Cannot create Trap npc as it is not correctly assigned as a BattleNPC type.");

        if (!npc.Owner.TryGet<WorldObject>(out var owner))
        {
            ServerLogger.LogWarning($"Npc {npc.Character} running trap init but does not have an owner.");
            return;
        }

        if (InheritOwnerFacing)
        {
            var angle = owner.Position.Angle(npc.Character.Position);
            var dir = Directions.GetFacingForAngle(angle);
            npc.Character.FacingDirection = dir;
        }

        npc.RevealAsEffect(EffectType(), "");
        npc.ValuesInt[0] = param1;
        npc.ValuesInt[2] = 0;


        var ce = npc.Character.CombatEntity;
        ce.SetStat(CharacterStat.MaxHp, 5);
        ce.SetStat(CharacterStat.Hp, 5);

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

    public override bool CanBeAttacked(Npc npc, BattleNpc battleNpc, CombatEntity attacker, CharacterSkill skill = CharacterSkill.None)
    {
        if (!Attackable)
            return false;
        if (attacker.Character.Type == CharacterType.Monster)
            return false;
        if (skill == CharacterSkill.None) return AllowAutoAttackMove;

        var attr = SkillHandler.GetSkillAttributes(skill);
        return attr.SkillTarget == SkillTarget.Ground && attr.SkillClassification == SkillClass.Physical;
    }

    public override void OnCalculateDamage(Npc npc, BattleNpc battleNpc, CombatEntity attacker, ref DamageInfo di)
    {
        di.Result = AttackResult.Invisible;
        //di.Damage = 1;

        if (di.AttackSkill == CharacterSkill.None && AllowAutoAttackMove)
            di.KnockBack = 3;

        if (attacker.Character.Type == CharacterType.Player)
            attacker.Player.AutoAttackLock = false;
    }

    public override void OnApplyDamage(Npc npc, BattleNpc battleNpc, ref DamageInfo di)
    {
        di.Damage = 0;
        if (di.KnockBack > 0 && npc.AreaOfEffect != null)
        {
            npc.Character.Map?.MoveAreaOfEffect(npc.AreaOfEffect, Area.CreateAroundPoint(npc.Character.Position, 1));
        }
    }

    public override void OnTimer(Npc npc, float lastTime, float newTime)
    {
        if (newTime > Duration(npc.ValuesInt[0]))
        {
            OnNaturalExpiration(npc);
            npc.EndEvent();
        }

        if (npc.ValuesInt[2] > 0)
            npc.EndEvent();
    }

    public virtual void OnNaturalExpiration(Npc npc) { }

    protected void HunterTrapExpiration(Npc npc)
    {
        if (npc.Owner.TryGet<WorldObject>(out var owner) && owner.Type != CharacterType.Player)
            return;

        var item = new GroundItem(npc.Character.Position, 1065, 1);
        npc.Character.Map?.DropGroundItem(ref item);
    }

    public abstract bool TriggerTrap(Npc npc, CombatEntity src, CombatEntity target, int skillLevel);

    public override void OnAoEInteraction(Npc npc, CombatEntity target, AreaOfEffect aoe)
    {
        if (npc.Character == target.Character || target.Character.ClassId == 3999)
            return;

        if (BlockMultipleActivations && npc.ValuesInt[2] > 0)
            return;

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