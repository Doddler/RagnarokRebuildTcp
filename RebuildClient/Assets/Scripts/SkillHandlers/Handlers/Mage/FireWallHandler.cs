using Assets.Scripts.Effects.EffectHandlers;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using RebuildSharedData.Enum;
using RebuildSharedData.Enum.EntityStats;
using UnityEngine;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.FireWall, true)]
    public class FireWallHandler : SkillHandlerBase
    {
        public override void StartSkillCasting(ServerControllable src, Vector2Int target, int lvl, float castTime)
        {
            HoldStandbyMotionForCast(src, castTime);
            src.AttachEffect(CastEffect.Create(castTime, src.gameObject, AttackElement.Fire));
            
            var targetCell = CameraFollower.Instance.WalkProvider.GetWorldPositionForTile(target);
            if(target != Vector2Int.zero)
                CastTargetCircle.Create(src.IsAlly, targetCell, 1, castTime);
        }
        
        public override void StartSkillCasting(ServerControllable src, ServerControllable target, int lvl, float castTime)
        {
            HoldStandbyMotionForCast(src, castTime);
            src.AttachEffect(CastEffect.Create(castTime, src.gameObject, AttackElement.Fire));
            
            if(target != null)
                target.AttachEffect(CastLockOnEffect.Create(castTime, target.gameObject));
        }

        public override void ExecuteSkillGroundTargeted(ServerControllable src, ref AttackResultData attack)
        {
            src.PerformSkillMotion();
            AudioManager.Instance.OneShotSoundEffect(src.Id, $"ef_firewall.ogg", attack.TargetAoE.ToWorldPosition());
        }

        public override void OnHitEffect(ServerControllable target, ref AttackResultData attack)
        {
            //Debug.Log("OnHitEffect Firewall");
            CameraFollower.Instance.AttachEffectToEntity("firehit1", target.gameObject, target.Id);
        }
    }
}