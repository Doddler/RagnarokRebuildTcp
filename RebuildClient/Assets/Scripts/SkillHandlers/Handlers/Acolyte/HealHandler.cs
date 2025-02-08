using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.Heal, true)]
    public class HealHandler : SkillHandlerBase
    {
        public override void OnHitEffect(ServerControllable target, ref AttackResultData attack)
        {
            attack.Target?.Messages.SendHitEffect(attack.Src, attack.MotionTime, 2);
        }

        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            src?.PerformSkillMotion();

            if (attack.Target == null)
                return;
            
            Debug.Log("Heal: " + attack.Damage);
            
            switch (-attack.Damage)
            {
                case < 200:
                    HealEffect.Create(attack.Target.gameObject, 0);
                    return;
                case < 2000:
                    HealEffect.Create(attack.Target.gameObject, 1);
                    return;
                default:
                    HealEffect.Create(attack.Target.gameObject, 2);
                    break;
            }


        }
    }
}