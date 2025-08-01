using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster;

[SkillHandler(CharacterSkill.ComboAttack)]
public class ComboAttackHandler : SkillHandlerBase
{
    public override int GetSkillRange(CombatEntity source, int lvl) => 8;

    //level 1: 2 hits @ 0.7x = 1.4x
    //level 2: 3 hits @ 0.6x = 1.8x
    //level 3: 4 hits @ 0.55x = 2.2x
    //level 4: 5 hits @ 0.52x = 2.6x
    //level 5: 6 hits @ 0.5x = 3x
    //level 6: 7 hits @ 0.485x = 3.4x
    //level 7: 8 hits @ 0.47x = 3.8x
    //level 8: 9 hits @ 0.46x = 4.2x
    //level 9: 10 hits @ 0.46x = 4.6x
    //level 10: 11 hits @ 0.45x = 5x
    //actual damage will vary as def/vit is applied per hit
    
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        lvl = lvl.Clamp(1, 10);

        if (target == null || !target.IsValidTarget(source))
            return;

        var total = 1f + lvl * 0.4f;
        var perHit = total / (lvl + 1);

        var req = new AttackRequest(CharacterSkill.ComboAttack, perHit, lvl + 1, AttackFlags.Physical, AttackElement.None);
        req.AccuracyRatio = 120;
        var res = source.CalculateCombatResult(target, req);
        
        source.ApplyCooldownForAttackAction(target);
        source.ExecuteCombatResult(res, false);
        
        CommandBuilder.SkillExecuteTargetedSkillAutoVis(source.Character, target.Character, CharacterSkill.ComboAttack, lvl, res);
    }
}