using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Network;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.Heal, true)]
    public class HealHandler : SkillHandlerBase
    {
        public override void ExecuteSkillTargeted(ServerControllable src, ServerControllable target, int lvl, int damage)
        {
            src?.PerformSkillMotion();
            switch (-damage)
            {
                case < 200:
                    HealEffect.Create(target.gameObject, 0);
                    return;
                case < 2000:
                    HealEffect.Create(target.gameObject, 1);
                    return;
                default:
                    HealEffect.Create(target.gameObject, 2);
                    break;
            }
        }
    }
}