using JetBrains.Annotations;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills;

[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public abstract class SkillHandlerBase
{
    public CharacterSkill Skill;
    public SkillClass SkillClassification = SkillClass.Unique;
    protected const int DefaultMagicCastRange = 9;

    public virtual bool IsAreaTargeted => false;
    public virtual float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl) => 0f;
    public virtual int GetAreaOfEffect(CombatEntity source, Position position, int lvl) => 0;
    public abstract void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect);

    public float GetCastTime(CombatEntity source, CombatEntity? target, int lvl) => GetCastTime(source, target, Position.Invalid, lvl);
    public float GetCastTime(CombatEntity source, Position position, int lvl) => GetCastTime(source, null, position, lvl);
    public void Process(CombatEntity source, Position position, int lvl, bool isIndirect = false) => Process(source, null, position, lvl, isIndirect);
    public void Process(CombatEntity source, CombatEntity target, int lvl, bool isIndirect = false) => Process(source, target, Position.Invalid, lvl, isIndirect);
    public virtual void ApplyPassiveEffects(CombatEntity owner, int lvl) { }
    public virtual void RemovePassiveEffects(CombatEntity owner, int lvl) { }

    public virtual int GetSkillRange(CombatEntity source, int lvl)
    {
        switch (SkillClassification)
        {
            case SkillClass.Magic: return DefaultMagicCastRange;
            default: return -1;
        }
    }

    public SkillValidationResult ValidateTargetForAmmunitionWeapon(CombatEntity source, CombatEntity? target, Position position, int weaponClass, AmmoType ammoType)
    {
        if (source.Character.Type != CharacterType.Player)
            return StandardValidation(source, target, position);
        if (source.Player.WeaponClass != weaponClass)
            return SkillValidationResult.IncorrectWeapon;
        var equip = source.Player.Equipment;
        if (equip.AmmoId <= 0 || equip.AmmoType != ammoType)
            return SkillValidationResult.IncorrectAmmunition;

        return StandardValidation(source, target, position);
    }

    public virtual SkillValidationResult StandardValidation(CombatEntity source, CombatEntity? target, Position position)
    {
        if (target != null)
        {
            if (source.Character.Map != null && !source.Character.Map.WalkData.HasLineOfSight(source.Character.Position, target.Character.Position))
                return SkillValidationResult.NoLineOfSight;

            if (target.IsValidTarget(source) || source == target)
                return SkillValidationResult.Success;

            return SkillValidationResult.InvalidTarget;
        }

        if (IsAreaTargeted)
        {
            if (source.Character.Map != null && !source.Character.Map.WalkData.HasLineOfSight(source.Character.Position, position))
                return SkillValidationResult.NoLineOfSight;

            return SkillValidationResult.Success;
        }

        return SkillValidationResult.Failure;
    }

    public virtual SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position) =>
        StandardValidation(source, target, position);

    protected void GenericCastAndInformSelfSkill(WorldObject ch, CharacterSkill skill, int level)
    {
        ch.Map?.AddVisiblePlayersAsPacketRecipients(ch);
        CommandBuilder.SkillExecuteSelfTargetedSkill(ch, skill, level);
        //CommandBuilder.SendEffectOnCharacterMulti(ch, DataManager.EffectIdForName["TwoHandQuicken"]); //Two Hand Quicken
        CommandBuilder.ClearRecipients();
    }

    protected void GenericCastAndInformSupportSkill(CombatEntity source, CombatEntity? target, CharacterSkill skill, int lvl, ref readonly DamageInfo damage)
    {
        source.Character.Map?.AddVisiblePlayersAsPacketRecipients(source.Character);
        CommandBuilder.SkillExecuteTargetedSkill(source.Character, target?.Character, skill, lvl, damage);
        CommandBuilder.ClearRecipients();

        if (source.Character.Type == CharacterType.Player)
            source.ApplyCooldownForSupportSkillAction();
        else
            source.ApplyCooldownForAttackAction();
    }
}

public class SkillHandlerAttribute : Attribute
{
    public CharacterSkill SkillType;
    public SkillClass SkillClassification;
    public SkillTarget SkillTarget;

    public SkillHandlerAttribute(CharacterSkill skillType, SkillClass skillClassification = SkillClass.None, SkillTarget skillTarget = SkillTarget.Enemy)
    {
        SkillType = skillType;
        SkillClassification = skillClassification;
        SkillTarget = skillTarget;
    }
}

//monsters may have different targeting modes... but we don't extend SkillHandlerAttribute because it breaks reflection in certain cases
public class MonsterSkillHandlerAttribute : Attribute
{
    public CharacterSkill SkillType;
    public SkillClass SkillClassification;
    public SkillTarget SkillTarget;

    public MonsterSkillHandlerAttribute(CharacterSkill skillType, SkillClass skillClassification = SkillClass.None, SkillTarget skillTarget = SkillTarget.Enemy)
    {
        SkillType = skillType;
        SkillClassification = skillClassification;
        SkillTarget = skillTarget;
    }
}


public struct SkillCastInfo
{
    public Entity TargetEntity;
    public Position TargetedPosition;
    public CharacterSkill Skill;
    public int Level;
    public float CastTime;
    public sbyte Range { get; set; }
    public bool IsIndirect { get; set; }
    public bool HideName { get; set; }

    public bool IsValid => Level > 0 && Level <= 30;
    public void Clear() { Level = 0; Range = -1; IsIndirect = false; HideName = false; }
}