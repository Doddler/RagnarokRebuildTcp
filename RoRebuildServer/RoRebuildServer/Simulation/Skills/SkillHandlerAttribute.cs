using JetBrains.Annotations;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Monsters;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills;

[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public abstract class SkillHandlerBase
{
    public CharacterSkill Skill;
    public SkillClass SkillClassification = SkillClass.Unique;
    protected const int DefaultMagicCastRange = 9;

    protected const int BlueGemstone = 717; //blue gemstone

    public virtual bool IsAreaTargeted => false;
    public virtual bool UsableWhileHidden => false;
    public virtual bool ShouldSkillCostSp(CombatEntity source) => true;
    public virtual float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl) => 0f;
    public virtual int GetAreaOfEffect(CombatEntity source, Position position, int lvl) => 0;

    public virtual bool PreProcessValidation(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect) => true;
    public abstract void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect);
    public virtual void NpcProcess(Npc source, CombatEntity? target, Position position, int lvl) { }


    public float GetCastTime(CombatEntity source, CombatEntity? target, int lvl) => GetCastTime(source, target, Position.Invalid, lvl);
    public float GetCastTime(CombatEntity source, Position position, int lvl) => GetCastTime(source, null, position, lvl);
    public void Process(CombatEntity source, Position position, int lvl, bool isIndirect = false) => Process(source, null, position, lvl, isIndirect);
    public void Process(CombatEntity source, CombatEntity target, int lvl, bool isIndirect = false) => Process(source, target, Position.Invalid, lvl, isIndirect);
    public virtual void ApplyPassiveEffects(CombatEntity owner, int lvl) { }
    public virtual void RemovePassiveEffects(CombatEntity owner, int lvl) { }
    public virtual void RefreshPassiveEffects(CombatEntity owner, int lvl) { }

    public virtual int GetSkillRange(CombatEntity source, int lvl)
    {
        switch (SkillClassification)
        {
            case SkillClass.Magic: return DefaultMagicCastRange;
            default: return -1;
        }
    }

    public bool ConsumeGemstoneForSkillWithFailMessage(CombatEntity source, int itemId)
    {
        if (source.Character.Type != CharacterType.Player)
            return true;

        if (source.GetStat(CharacterStat.NoGemstone) <= 0 && !source.Player.TryRemoveItemFromInventory(itemId, 1, true))
        {
            CommandBuilder.SkillFailed(source.Player, SkillValidationResult.MissingRequiredItem);
            return false;
        }

        return true;
    }

    public bool CheckRequiredGemstone(CombatEntity source, int itemId, bool sendFailMessage = true)
    {
        if (source.Character.Type == CharacterType.Player && (source.Player.Inventory == null || !source.Player.Inventory.HasItem(itemId)))
        {
            if (source.GetStat(CharacterStat.NoGemstone) > 0)
                return true;

            if(sendFailMessage)
                CommandBuilder.SkillFailed(source.Player, SkillValidationResult.MissingRequiredItem);
            return false;
        }

        return true;
    }

    public bool CheckRequiredItem(CombatEntity source, int itemId, bool sendFailMessage = true)
    {
        if (source.Character.Type == CharacterType.Player && (source.Player.Inventory == null || !source.Player.Inventory.HasItem(itemId)))
        {
            CommandBuilder.SkillFailed(source.Player, SkillValidationResult.MissingRequiredItem);
            return false;
        }

        return true;
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


    public virtual SkillValidationResult StandardValidationForAllyTargetedAttack(CombatEntity source, CombatEntity? target, Position position)
    {
        if (source.Character.Type == CharacterType.Player)
        {
            if (!UsableWhileHidden && source.HasBodyState(BodyStateFlags.Hidden))
                return SkillValidationResult.UnusableWhileHidden;
        }

        if (target != null)
        {
            if (source.Character.Map != null && !source.Character.Map.WalkData.HasLineOfSight(source.Character.Position, target.Character.Position))
                return SkillValidationResult.NoLineOfSight;

            if (target.IsValidAlly(source) || source == target)
                return SkillValidationResult.Success;

            if (SkillClassification == SkillClass.Magic && target.GetStat(CharacterStat.MagicImmunity) > 0)
                return SkillValidationResult.TargetImmuneToEffect;

            return SkillValidationResult.InvalidTarget;
        }

        if (position.IsValid())
        {
            if (source.Character.Map != null && !source.Character.Map.WalkData.HasLineOfSight(source.Character.Position, position))
                return SkillValidationResult.NoLineOfSight;

            return SkillValidationResult.Success;
        }

        return SkillValidationResult.Failure;
    }

    public virtual SkillValidationResult StandardValidation(CombatEntity source, CombatEntity? target, Position position)
    {
        if (source.Character.Type == CharacterType.Player)
        {
            if (!UsableWhileHidden && source.HasBodyState(BodyStateFlags.Hidden))
                return SkillValidationResult.UnusableWhileHidden;
        }

        if (target != null)
        {
            if (source.Character.Map != null && !source.Character.Map.WalkData.HasLineOfSight(source.Character.Position, target.Character.Position))
                return SkillValidationResult.NoLineOfSight;
            
            if (target.IsValidTarget(source) || target.IsValidAlly(source) || source == target)
                return SkillValidationResult.Success;

            return SkillValidationResult.InvalidTarget;
        }

        if (position.IsValid())
        {
            if (source.Character.Map != null && !source.Character.Map.WalkData.HasLineOfSight(source.Character.Position, position))
                return SkillValidationResult.NoLineOfSight;

            return SkillValidationResult.Success;
        }

        return SkillValidationResult.Failure;
    }

    public virtual SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect) =>
        StandardValidation(source, target, position);

    protected void GenericCastAndInformSelfSkill(WorldObject ch, CharacterSkill skill, int level)
    {
        ch.Map?.AddVisiblePlayersAsPacketRecipients(ch);
        CommandBuilder.SkillExecuteSelfTargetedSkill(ch, skill, level, false);
        //CommandBuilder.SendEffectOnCharacterMulti(ch, DataManager.EffectIdForName["TwoHandQuicken"]); //Two Hand Quicken
        CommandBuilder.ClearRecipients();
    }

    protected void GenericCastAndInformSupportSkill(CombatEntity source, CombatEntity? target, CharacterSkill skill, int lvl, ref readonly DamageInfo damage, bool isIndirect, bool applyCooldown = false)
    {
        source.Character.Map?.AddVisiblePlayersAsPacketRecipients(source.Character);
        CommandBuilder.SkillExecuteTargetedSkill(source.Character, target?.Character, skill, lvl, damage);
        CommandBuilder.ClearRecipients();

        if (!applyCooldown)
            return;

        if (source.Character.Type == CharacterType.Player)
            source.ApplyCooldownForSupportSkillAction();
        else
            source.ApplyCooldownForAttackAction();
    }

    protected void ApplySkillCooldown()
    {

    }
}

public class SkillHandlerAttribute : Attribute
{
    public CharacterSkill SkillType;
    public SkillClass SkillClassification;
    public SkillTarget SkillTarget;
    public SkillPreferredTarget SkillPreferredTarget;

    public SkillHandlerAttribute(CharacterSkill skillType, SkillClass skillClassification = SkillClass.None, SkillTarget skillTarget = SkillTarget.Enemy, SkillPreferredTarget preferredTarget = SkillPreferredTarget.Any)
    {
        SkillType = skillType;
        SkillClassification = skillClassification;
        SkillTarget = skillTarget;
        SkillPreferredTarget = preferredTarget;
        if (preferredTarget == SkillPreferredTarget.Any)
        {
            SkillPreferredTarget = skillTarget switch
            {
                SkillTarget.Enemy => SkillPreferredTarget.Enemy,
                SkillTarget.Ally => SkillPreferredTarget.Self,
                SkillTarget.Self => SkillPreferredTarget.Self,
                _ => SkillPreferredTarget.Enemy
            };
        }
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

public struct SkillCastInfo()
{
    public Entity TargetEntity;
    public Position TargetedPosition;
    public int Level;
    public float CastTime;
    //public float AfterCastDelay;
    //public float CooldownTime;
    public short ItemSource = -1;
    public CharacterSkill Skill;
    public sbyte Range { get; set; } = -1;
    public SkillCastFlags Flags;
    public bool IsIndirect { get; set; }

    public bool IsValid => Level > 0 && Level <= 30;
    public void Clear() => this = default; //{ Level = 0; Range = -1; ItemSource = -1; IsIndirect = false; HideName = false; }
}