using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Thief;

[SkillHandler(CharacterSkill.Hiding, SkillClass.None, SkillTarget.Self)]
public class HidingHandler : SkillHandlerBase
{
    public override bool UsableWhileHidden => true;

    public override bool ShouldSkillCostSp(CombatEntity source)
    {
        return !source.HasStatusEffectOfType(CharacterStatusEffect.Hiding);
    }

    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        var ch = source.Character;

        source.ApplyCooldownForSupportSkillAction();

        CommandBuilder.SkillExecuteSelfTargetedSkillAutoVis(ch, CharacterSkill.Hiding, lvl, isIndirect);

        if (source.HasStatusEffectOfType(CharacterStatusEffect.Hiding))
        {
            source.RemoveStatusOfTypeIfExists(CharacterStatusEffect.Hiding);
            return;
        }

        var time = 30f + 15f * lvl;
        if (source.Character.Type == CharacterType.Monster)
            time = 16f + 5f * lvl;
        
        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.Hiding, time, lvl);
        source.AddStatusEffect(status);
    }
}