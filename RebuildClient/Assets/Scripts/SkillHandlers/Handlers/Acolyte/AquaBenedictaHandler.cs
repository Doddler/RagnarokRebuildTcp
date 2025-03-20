using Assets.Scripts.Effects.EffectHandlers.General;
using Assets.Scripts.Network;
using Assets.Scripts.Objects;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers.Handlers
{
    [SkillHandler(CharacterSkill.AquaBenedicta)]
    public class AquaBenedictaHandler : SkillHandlerBase
    {
        public override void ExecuteSkillTargeted(ServerControllable src, ref AttackResultData attack)
        {
            src.PerformSkillMotion();
            var effect = RoSpriteEffect.AttachSprite(src, "Assets/Sprites/Effects/성수뜨기.spr", 1f, 1f, RoSpriteEffectFlags.EndWithAnimation);
            effect.SetDurationByFrames(90);
            src.AttachEffect(effect);
            AudioManager.Instance.AttachSoundToEntity(src.Id, "ef_aqua.ogg", src.gameObject);
        }
    }
}