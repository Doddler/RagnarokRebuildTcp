using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Network;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using UnityEngine;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.Sanctuary)]
    public class SanctuaryHandler : SkillHandlerBase
    {
        public override void StartSkillCasting(ServerControllable src, Vector2Int target, int lvl, float castTime)
        {
            HoldStandbyMotionForCast(src, castTime);
            src.AttachEffect(CastEffect.Create(castTime, src.gameObject, AttackElement.Ghost));
            
            var targetCell = CameraFollower.Instance.WalkProvider.GetWorldPositionForTile(target);
            if(target != Vector2Int.zero)
                CastTargetCircle.Create(src.IsAlly, targetCell, 3, castTime);
        }
        
        public override void StartSkillCasting(ServerControllable src, ServerControllable target, int lvl, float castTime)
        {
            HoldStandbyMotionForCast(src, castTime);
            src.AttachEffect(CastEffect.Create(castTime, src.gameObject, AttackElement.Ghost));
            
            if(target != null)
                target.AttachEffect(CastLockOnEffect.Create(castTime, target.gameObject));
        }

        public override void ExecuteSkillGroundTargeted(ServerControllable src, ref AttackResultData attack)
        {
            src.PerformSkillMotion();
            CameraFollower.Instance.CreateEffectAtLocation("Sanctuary", attack.TargetAoE.ToWorldPosition(), new Vector3(1.5f, 1.5f, 1.5f), 0);
            //AudioManager.Instance.OneShotSoundEffect(src.Id, $"priest_sanctuary.ogg", attack.TargetAoE.ToWorldPosition());
        }

        public override void OnHitEffect(ServerControllable target, ref AttackResultData attack)
        {
            target.Messages.SendElementalHitEffect(attack.Src, attack.DamageTiming, AttackElement.Holy, attack.HitCount);
        }
    }
}