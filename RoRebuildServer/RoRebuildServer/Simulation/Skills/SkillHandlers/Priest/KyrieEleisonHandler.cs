using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Priest;

[SkillHandler(CharacterSkill.KyrieEleison, SkillClass.Magic, SkillTarget.Ally)]
public class KyrieEleisonHandler : SkillHandlerBase
{
    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl) => 2;

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect, bool isItemSource)
    {
        if (target == null)
            return;

        if (!isIndirect)
            source.ApplyAfterCastDelay(2f);

        var blockCount = lvl switch
        {
            1 => 5,
            2 or 3 => 6,
            4 or 5 => 7,
            6 or 7 => 8,
            8 or 9 => 9,
            _ => 10
        };

        var barrier = target.GetStat(CharacterStat.MaxHp) * (10 + lvl * 2) / 100;

        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.KyrieEleison, 120, barrier, blockCount);
        target.AddStatusEffect(status);

        var res = DamageInfo.SupportSkillResult(source.Entity, target.Entity, CharacterSkill.KyrieEleison);
        GenericCastAndInformSupportSkill(source, target, CharacterSkill.KyrieEleison, lvl, ref res, isIndirect, true);
    }
}