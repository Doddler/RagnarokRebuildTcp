using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntitySystem;

namespace RoRebuildServer.Simulation.Skills;

public abstract class SkillHandlerBase
{
    public SkillClass SkillClassification = SkillClass.Unique;
    protected const int DefaultMagicCastRange = 9;

    public virtual bool IsAreaTargeted => false;
    public virtual float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl) => 0f;
    public abstract void Process(CombatEntity source, CombatEntity? target, Position position, int lvl);

    public float GetCastTime(CombatEntity source, CombatEntity? target, int lvl) => GetCastTime(source, target, Position.Invalid, lvl);
    public float GetCastTime(CombatEntity source, Position position, int lvl) => GetCastTime(source, null, position, lvl);
    public void Process(CombatEntity source, Position position, int lvl) => Process(source, null, position, lvl);
    public void Process(CombatEntity source, CombatEntity target, int lvl) => Process(source, target, Position.Invalid, lvl);

    public virtual int GetSkillRange(CombatEntity source, int lvl)
    {
        switch (SkillClassification)
        {
            case SkillClass.Magic: return DefaultMagicCastRange;
            default: return -1;
        }
    }

    public virtual SkillValidationResult ValidateTarget(CombatEntity source, CombatEntity? target, Position position)
    {
        if (target != null)
        {
            if (source.Character.Map != null && !source.Character.Map.WalkData.HasLineOfSight(source.Character.Position, target.Character.Position))
                return SkillValidationResult.NoLineOfSight;

            if (target.IsValidTarget(source))
                return SkillValidationResult.Success;

            return SkillValidationResult.InvalidTarget;
        }

        if (IsAreaTargeted)
        {
            if (source.Character.Map != null && !source.Character.Map.WalkData.HasLineOfSight(source.Character.Position, position))
                return SkillValidationResult.NoLineOfSight;
        }
            
        return SkillValidationResult.Failure;
    }
}

public class SkillHandlerAttribute : Attribute
{
    public CharacterSkill SkillType;
    public SkillClass SkillClassification;
    public SkillTarget SkillTarget;

    public SkillHandlerAttribute(CharacterSkill skillType, SkillClass skillClassification = SkillClass.None, SkillTarget skillTarget = SkillTarget.SingleTarget)
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

    public bool IsValid => Level > 0 && Level <= 30;
    public void Clear() { Level = 0; }
}