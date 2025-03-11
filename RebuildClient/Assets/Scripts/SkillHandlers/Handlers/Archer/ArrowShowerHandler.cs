using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Effects.EffectHandlers.General;
using Assets.Scripts.MapEditor;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.ArrowShower)]
    public class ArrowShowerHandler : SkillHandlerBase
    {
        public override int GetSkillAoESize(ServerControllable src, int lvl) => 3;
        
        public override void OnHitEffect(ServerControllable target, ref AttackResultData attack)
        {
            attack.Target.Messages.SendHitEffect(attack.Src, attack.DamageTiming, 2, 1);
        }

        public override void ExecuteSkillGroundTargeted(ServerControllable src, ref AttackResultData attack)
        {
            DefaultSkillCastEffect.Create(src);
            src.PerformBasicAttackMotion();
            AudioManager.Instance.AttachSoundToEntity(src.Id, "ef_bash.ogg", src.gameObject);

            if (attack.TargetAoE == Vector2Int.zero)
                return;
            
            //Debug.Break();
            //Time.timeScale = 0.1f;
            for (var i = 0; i < 9; i++)
            {
                var x = attack.TargetAoE.x + Random.Range(-2f, 2f);
                var y = attack.TargetAoE.y + Random.Range(-2f, 2f);

                var target = new Vector3(x, RoWalkDataProvider.Instance.GetHeightForPosition(x, y), y);
                // Debug.Log($"ArrowShower {i}: {target}");
                
                ArcherArrow.CreateArrow(src, target, attack.MotionTime, -0.1f + Random.Range(-0.1f, 0f));      
            }
            
        }
    }
}