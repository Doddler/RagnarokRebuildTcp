using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Logging;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Acolyte;

[SkillHandler(CharacterSkill.DecreaseAgility, SkillClass.Magic, SkillTarget.Enemy)]
public class DecreaseAgiHandler : SkillHandlerBase
{
    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl) => 1f;

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        if (target == null)
        {
            ServerLogger.LogWarning($"Entity {source.Character.Name} is attempting to cast without a target.");
            return;
        }

        var chance = 50 + lvl * 3;
        var casterLevel = source.GetEffectiveStat(CharacterStat.Level);
        var casterInt = source.GetEffectiveStat(CharacterStat.Int);
        var targetMDef = target.GetEffectiveStat(CharacterStat.MDef);
        var res = DamageInfo.SupportSkillResult(source.Entity, target.Entity, CharacterSkill.DecreaseAgility);

        chance += (casterLevel + casterInt) / 5 - targetMDef;
        if (target.GetSpecialType() == CharacterSpecialType.Boss || GameRandom.Next(0, 100) >= chance)
        {
            res.Result = AttackResult.Miss;
        }
        else
        {
            if (!isIndirect)
            {
                source.ApplyCooldownForSupportSkillAction();
                source.ApplyAfterCastDelay(1f);
            }

            var duration = 30 + 10 * lvl; //60s-240s
            if (target.Character.Type == CharacterType.Player)
                duration /= 2;

            var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.DecreaseAgi, duration, lvl);
            target.AddStatusEffect(status);
        }

        GenericCastAndInformSupportSkill(source, target, CharacterSkill.DecreaseAgility, lvl, ref res, isIndirect);
    }
}