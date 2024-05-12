using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.SkillHandlers
{
    public static partial class ClientSkillHandler
    {
        private static SkillHandlerBase[] handlers;

        public static void StartCastingSkill(ServerControllable src, ServerControllable target, CharacterSkill skillId, int lvl, float castTime) =>
            handlers[(int)skillId].StartSkillCasting(src, target, lvl, castTime);
        
        public static void StartCastingSkill(ServerControllable src, Vector2Int target, CharacterSkill skillId, int lvl, float castTime) =>
            handlers[(int)skillId].StartSkillCasting(src, target, lvl, castTime);

        public static void ExecuteSkill(ServerControllable src, ServerControllable target, CharacterSkill skillId, int lvl)
        {
            var handler = handlers[(int)skillId];

            if (src == null && !handler.ExecuteWithoutSource)
                return;
            
            var targetType = ClientDataLoader.Instance.GetSkillTarget(skillId);

            if(targetType == SkillTarget.AreaTargeted)
                handler.ExecuteSkillGroundTargeted(src, Vector2Int.zero, lvl); //need to fix this whole thing to have the target position
            else
                handler.ExecuteSkillTargeted(src, target, lvl);
        }
            
    }
}