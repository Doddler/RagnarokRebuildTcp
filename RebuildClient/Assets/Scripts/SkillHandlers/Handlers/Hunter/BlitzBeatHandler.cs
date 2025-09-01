using Assets.Scripts.Misc;
using Assets.Scripts.Network;
using RebuildSharedData.Enum;

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
            if(bird != null)
                bird.LaunchAttackTarget(attack.Target);
        }
    }
}