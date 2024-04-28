using Assets.Scripts.Network;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers
{
    public static partial class ClientSkillHandler
    {
        private static SkillHandlerBase[] handlers;

        public static void StartCastingSkill(ServerControllable src, ServerControllable target, CharacterSkill skillId, int lvl, float castTime) =>
            handlers[(int)skillId].StartSkillCasting(src, target, SkillTarget.SingleTarget, lvl, castTime);

        public static void ExecuteSkill(ServerControllable src, ServerControllable target, CharacterSkill skillId, int lvl)
        {
            var handler = handlers[(int)skillId];

            if (src == null && !handler.ExecuteWithoutSource)
                return;

            handler.ExecuteSkillTargeted(src, target, lvl);
        }
            
    }
}