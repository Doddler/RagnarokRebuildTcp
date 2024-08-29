using Assets.Scripts.Network;
using Assets.Scripts.Network.HandlerBase;
using Assets.Scripts.Objects;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.IncreaseAgility)]
    public class IncreaseAgiHandler : SkillHandlerBase
    {
        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            src?.PerformSkillMotion();
            attack.Target?.AttachFloatingTextIndicator("<font-weight=300><cspace=-0.5>AGI UP!");
            if(src != null)
                AudioManager.Instance.AttachSoundToEntity(src.Id, "ef_incagility.ogg", src.gameObject);
        }
    }
}