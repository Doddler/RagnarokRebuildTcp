using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Assassin;

[SkillHandler(CharacterSkill.EnchantPoison, SkillClass.Physical, SkillTarget.Ally)]
public class EnchantPoisonHandler : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        if (target == null)
            return;

        if (!isIndirect)
            source.ApplyAfterCastDelay(1f);

        var chance = (2 + (1 + lvl) / 2) * 10;

        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.EnchantPoison, 15 + 15 * lvl, 5, -1, (byte)chance);
        target.AddStatusEffect(status);

        var res = DamageInfo.SupportSkillResult(source.Entity, target.Entity, CharacterSkill.EnchantPoison);
        GenericCastAndInformSupportSkill(source, target, CharacterSkill.EnchantPoison, lvl, ref res, isIndirect, true);
    }
}