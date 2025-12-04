using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster;

[SkillHandler(CharacterSkill.WindAttack, SkillClass.Physical)]
public class WindAttackHandler : ElementalAttackHandler
{
    public override AttackElement GetElement() => AttackElement.Wind;
    public override CharacterSkill GetSkill() => CharacterSkill.WindAttack;
}

[SkillHandler(CharacterSkill.FireAttack, SkillClass.Physical)]
public class FireAttackHandler : ElementalAttackHandler
{
    public override AttackElement GetElement() => AttackElement.Fire;
    public override CharacterSkill GetSkill() => CharacterSkill.FireAttack;
}

[SkillHandler(CharacterSkill.IceAttack, SkillClass.Physical)]
public class IceAttackHandler : ElementalAttackHandler
{
    public override AttackElement GetElement() => AttackElement.Water;
    public override CharacterSkill GetSkill() => CharacterSkill.IceAttack;
}

[SkillHandler(CharacterSkill.WaterAttack, SkillClass.Physical)]
public class WaterAttackHandler : ElementalAttackHandler
{
    public override AttackElement GetElement() => AttackElement.Water;
    public override CharacterSkill GetSkill() => CharacterSkill.WaterAttack;
}

[SkillHandler(CharacterSkill.EarthAttack, SkillClass.Physical)]
public class EarthAttackHandler : ElementalAttackHandler
{
    public override AttackElement GetElement() => AttackElement.Earth;
    public override CharacterSkill GetSkill() => CharacterSkill.EarthAttack;
}

[SkillHandler(CharacterSkill.GhostAttack, SkillClass.Physical)]
public class GhostAttackHandler : ElementalAttackHandler
{
    public override AttackElement GetElement() => AttackElement.Ghost;
    public override CharacterSkill GetSkill() => CharacterSkill.GhostAttack;
}

[SkillHandler(CharacterSkill.UndeadAttack, SkillClass.Physical)]
public class UndeadAttackHandler : ElementalAttackHandler
{
    public override AttackElement GetElement() => AttackElement.Undead;
    public override CharacterSkill GetSkill() => CharacterSkill.UndeadAttack;
}

[SkillHandler(CharacterSkill.PoisonAttack, SkillClass.Physical)]
public class PoisonAttackHandler : ElementalAttackHandler
{
    public override AttackElement GetElement() => AttackElement.Poison;
    public override CharacterSkill GetSkill() => CharacterSkill.PoisonAttack;
}

[SkillHandler(CharacterSkill.DarkAttack, SkillClass.Physical)]
public class DarkAttackHandler : ElementalAttackHandler
{
    public override AttackElement GetElement() => AttackElement.Dark;
    public override CharacterSkill GetSkill() => CharacterSkill.DarkAttack;
}

[SkillHandler(CharacterSkill.HolyAttack, SkillClass.Physical)]
public class HolyAttackHandler : ElementalAttackHandler
{
    public override AttackElement GetElement() => AttackElement.Holy;
    public override CharacterSkill GetSkill() => CharacterSkill.HolyAttack;
}

public abstract class ElementalAttackHandler : SkillHandlerBase
{
    public abstract CharacterSkill GetSkill();
    public abstract AttackElement GetElement();
    public override int GetSkillRange(CombatEntity source, int lvl) => 7;

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        if (target == null)
            return;

        var ratio = 0.8f + lvl * 0.6f;

        var element = GetElement();

        var res = source.CalculateCombatResult(target, ratio, 1, AttackFlags.Physical | AttackFlags.AutoRange, GetSkill(), element);
        if (element == AttackElement.Poison)
        {
            var poisonBarrier = res.IsDamageResult && target.RemoveStatusOfTypeIfExists(CharacterStatusEffect.Detoxify);
            if (poisonBarrier)
                res.Damage /= 2;
        }

        source.ApplyCooldownForAttackAction(target);
        source.ExecuteCombatResult(res, false);

        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, GetSkill(), lvl, res);
    }
}