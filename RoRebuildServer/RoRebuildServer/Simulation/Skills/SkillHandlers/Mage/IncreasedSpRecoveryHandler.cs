using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Mage;

[SkillHandler(CharacterSkill.IncreaseSPRecovery, SkillClass.Physical, SkillTarget.Passive)]
public class IncreasedSpRecoveryHandler : SkillHandlerBase
{
    public override void ApplyPassiveEffects(CombatEntity owner, int lvl)
    {
        //don't add AddSpRecoveryPercent here, it's calculated in a special way in Player.SpRegenTick() and has a unique visual
        owner.AddStat(CharacterStat.AddSpItemEffectivenessPercent, lvl * 10);
    }

    public override void RemovePassiveEffects(CombatEntity owner, int lvl)
    {
        owner.SubStat(CharacterStat.AddSpItemEffectivenessPercent, lvl * 10);
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        throw new NotImplementedException();
    }
}
