using Assets.Scripts.Misc;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.SkillHandlers.Handlers.Hunter
{
    [SkillHandler(CharacterSkill.BlitzBeat)]
    public class BlitzBeatHandler : SkillHandlerBase
    {
        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            if (src == null)
                return;
            
            src.PerformSkillMotion();
            var follower = src.FollowerObject;
            if (follower == null || attack.Target == null)
                return;
            var bird = follower.GetComponent<BirdFollower>();
            if (bird != null)
            {
                AudioManager.Instance.OneShotSoundEffect(src.Id, "hunter_blitzbeat_1st.ogg", src.transform.position, 0.8f);
                AudioManager.Instance.OneShotSoundEffect(src.Id, "hunter_blitzbeat.ogg", attack.Target.transform.position, 0.7f, 0.5f);
                bird.LaunchAttackTarget(attack.Target);
            }
        }
    }
}