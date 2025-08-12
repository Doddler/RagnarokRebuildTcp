using RebuildSharedData.Data;
using RebuildSharedData.Enum.EntityStats;
using RebuildSharedData.Enum;
using RoRebuildServer.Data;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;
using System.Diagnostics;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Wizard;

[SkillHandler(CharacterSkill.HeavensDrive, SkillClass.Magic, SkillTarget.Ground, SkillPreferredTarget.Enemy)]
public class HeavensDriveHandler : SkillHandlerBase
{
    public override int GetAreaOfEffect(CombatEntity source, Position position, int lvl) => 2; //range 2 = 5x5

    public override float GetCastTime(CombatEntity source, CombatEntity? target, Position position, int lvl) => 1f + lvl * 0.5f;
    
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect,
        bool isItemSource)
    {
        var map = source.Character.Map;
        Debug.Assert(map != null);

        Span<bool> effectiveArea = stackalloc bool[5 * 5];

        using var targetList = EntityListPool.Get();

        //gather all players who can see the spell effect and make them packet recipients
        map.GatherPlayersInRange(position, ServerConfig.MaxViewDistance + 2, targetList, false, false);
        CommandBuilder.AddRecipients(targetList);
        targetList.Clear(); //reused, first we use it to gather visible targets, then players hit by the aoe
        
        if (map.FillGroundAreaOfEffectMaskWithLoS(source.Character.Position, position, 2, ref effectiveArea))
        {
            map.GatherEnemiesInMaskedArea(source, position, 2, ref effectiveArea, targetList, true, true);

            //deal damage to all enemies
            foreach (var e in targetList)
            {
                if (!e.TryGet<CombatEntity>(out var blastTarget))
                    continue;

                var res = source.CalculateCombatResult(blastTarget, 1, lvl, AttackFlags.Magical | AttackFlags.CanAttackHidden,
                    CharacterSkill.HeavensDrive, AttackElement.Earth);
                source.ExecuteCombatResult(res, false);

                CommandBuilder.AttackMulti(source.Character, blastTarget.Character, res, false);
            }
        }

        source.ApplyCooldownForSupportSkillAction();

        var mt = source.GetAttackMotionTime();
        CommandBuilder.SkillExecuteMaskedAreaTargetedSkill(source.Character, position, 2, CharacterSkill.HeavensDrive, lvl, ref effectiveArea, isIndirect, mt);
    }
}