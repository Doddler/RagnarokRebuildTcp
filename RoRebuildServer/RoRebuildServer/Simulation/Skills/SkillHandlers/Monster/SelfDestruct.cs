using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Util;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;
using System.Diagnostics;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntitySystem;
using RoRebuildServer.Data;

namespace RoRebuildServer.Simulation.Skills.SkillHandlers.Monster;

[SkillHandler(CharacterSkill.SelfDestruct, SkillClass.Physical, SkillTarget.Self)]
public class SelfDestruct : SkillHandlerBase
{
    public override void Process(CombatEntity source, CombatEntity? target, Position position, int lvl, bool isIndirect)
    {
        var ch = source.Character;
        var map = source.Character.Map;
        Debug.Assert(map != null);

        var targetList = EntityListPool.Get();
        map.GatherEnemiesInArea(ch, ch.Position, 3, targetList, true, true);
        map.GatherAlliesInRange(ch, 3, targetList, true, true);
        
        var baseDamage = source.GetStat(CharacterStat.Hp) / 2;
        var minAtk = (int)(baseDamage * 0.8f);
        var maxAtk = (int)(baseDamage * 1.2f);
        
        foreach (var e in targetList)
        {
            var aoeTarget = e.Get<CombatEntity>();
            if (!aoeTarget.IsValidTarget(source, true))
                continue;

            var attack = new AttackRequest()
            {
                MinAtk = minAtk,
                MaxAtk = maxAtk,
                Element = AttackElement.Special,
                Flags = AttackFlags.Physical | AttackFlags.CanHarmAllies | AttackFlags.IgnoreDefense |
                        AttackFlags.IgnoreEvasion,
            };
            
            var res = source.CalculateCombatResultUsingSetAttackPower(e.Get<CombatEntity>(), attack);
            res.KnockBack = res.Damage switch
            {
                >5000 => 7,
                _ => 5
            };
            
            if(res.Damage > 0)
                source.ExecuteCombatResult(res, true, false);
        }

        EntityListPool.Return(targetList);

        map.AddVisiblePlayersAsPacketRecipients(ch);
        CommandBuilder.SendEffectAtLocationMulti(DataManager.EffectIdForName["Explosion"], ch.Position, 0);
        CommandBuilder.ClearRecipients();
        
        if (ch.Type == CharacterType.Player)
            ch.Player.Die();
        if(ch.Type == CharacterType.Monster)
            ch.Monster.Die();
    }
}

