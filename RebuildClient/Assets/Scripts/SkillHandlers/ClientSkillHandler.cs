using Assets.Scripts.Network;
using JetBrains.Annotations;
using RebuildSharedData.Enum;

namespace Assets.Scripts.SkillHandlers
{
    public static partial class ClientSkillHandler
    {
        private static SkillHandlerBase[] handlers;

        public static void StartCastingSkill(ServerControllable src, ServerControllable target, SkillType skillType, int lvl, float castTime) =>
            handlers[(int)skillType].StartSkillCasting(src, target, skillType, lvl, castTime);

        public static void ExecuteSkill(ServerControllable src, ServerControllable target, CharacterSkill skillId, int lvl) =>
            handlers[(int)skillId].ExecuteSkillTargeted(src, target, lvl);
    }
}