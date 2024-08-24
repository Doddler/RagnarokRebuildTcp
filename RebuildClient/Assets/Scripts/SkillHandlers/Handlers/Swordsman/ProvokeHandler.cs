using Assets.Scripts.Network;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.Provoke)]
    public class ProvokeHandler : SkillHandlerBase
    {
        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            if (attack.Target != null)
            {
                CameraFollower.Instance.AttachEffectToEntity("Provoke", attack.Target.gameObject, src.Id);
                src.LookAt(attack.Target.transform.position);
            }
            
            src.PerformSkillMotion();
        }
    }
}