using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.StatusEffects.Setup;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Swordsman;

[SkillHandler(CharacterSkill.Endure, SkillClass.None, SkillTarget.Self)]
public class EndureHandler : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        //val1 is hit count, val2 is mdef increase
        var status = StatusEffectState.NewStatusEffect(CharacterStatusEffect.Endure, 7f + lvl * 3f, 2 + lvl, lvl);
        source.AddStatusEffect(status);
        
        GenericCastAndInformSelfSkill(source.Character, CharacterSkill.Endure, lvl);
    }
}