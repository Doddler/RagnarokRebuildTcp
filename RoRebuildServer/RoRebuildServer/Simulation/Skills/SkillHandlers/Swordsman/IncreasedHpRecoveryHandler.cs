using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Swordsman;

[SkillHandler(CharacterSkill.IncreasedHPRecovery, SkillClass.Physical, SkillTarget.Passive)]
public class IncreasedHpRecoveryHandler : SkillHandlerBase
{
    public override void ApplyPassiveEffects(CombatEntity owner, int lvl)
    {
        //don't add AddHpRecoveryPercent here, it's calculated in a special way in Player.HpRegenTick() and has a unique visual
        owner.AddStat(CharacterStat.AddHpItemEffectivenessPercent, lvl * 10);
    }

    public override void RemovePassiveEffects(CombatEntity owner, int lvl)
    {
        owner.SubStat(CharacterStat.AddHpItemEffectivenessPercent, lvl * 10);
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        throw new NotImplementedException();
    }
}
