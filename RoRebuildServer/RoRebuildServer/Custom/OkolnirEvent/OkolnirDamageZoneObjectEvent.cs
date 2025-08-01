using RebuildSharedData.Data;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using RoRebuildServer.EntityComponents;
using RoRebuildServer.EntityComponents.Character;
using RoRebuildServer.EntityComponents.Npcs;
using RoRebuildServer.Networking;
using RoRebuildServer.Simulation.Util;

namespace RoRebuildServer.Custom.OkolnirEvent;

public class OkolnirDamageZoneObjectEvent : NpcBehaviorBase
{
    public override void InitEvent(Npc npc, int param1, int param2, int param3, int param4, string? paramString)
    {
        npc.StartTimer(250);

        base.InitEvent(npc, param1, param2, param3, param4, paramString);
    }

    public override void OnTimer(Npc npc, float lastTime, float newTime)
    {
        if (npc.Character.Map == null)
            return;

        if (newTime >= 0.5f)
        {
            //% (135, 133, 32, 34)
            using var list = EntityListPool.Get();
            npc.Character.Map.GatherPlayersInArea(Area.CreateAroundPoint(135, 133, 32, 34), list, true);

            //the center of the zone and the part that covers the north path need to not hurt players, so we exclude those players from taking damage
            var exclusion1 = new Area(116, 120, 147, 151);
            var exclusion2 = new Area(129, 160, 173, 175);

            foreach (var entity in list)
            {
                if (!entity.TryGet<Player>(out var player))
                    continue;
                var ch = player.Character;

                if (exclusion1.Contains(ch.Position) || exclusion2.Contains(ch.Position) || ch.AdminHidden)
                    continue;

                var hp = player.GetStat(CharacterStat.MaxHp);
                var status = player.CombatEntity.AddOrStackStatusEffect(CharacterStatusEffect.Vulnerability, 90, 99);

                var res = ch.CombatEntity.PrepareTargetedSkillResult(ch.CombatEntity, CharacterSkill.FireAttack);
                res.Damage = hp * (5 + status.Value4) / 150;
                res.HitCount = 1;
                res.AttackPosition = ch.Position;
                res.Time = 0;
                res.AttackMotionTime = 0;
                res.IsIndirect = true;
                res.Result = AttackResult.NormalDamage;
                res.Flags = DamageApplicationFlags.NoHitLock;

                CommandBuilder.SkillExecuteIndirectAutoVisibility(ch, ch, res);
                ch.CombatEntity.ExecuteCombatResult(res, false);
            }

            npc.TimerStart += 0.5f;
        }

        base.OnTimer(npc, lastTime, newTime);
    }
}