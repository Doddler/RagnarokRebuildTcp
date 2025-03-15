using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.SkillHandlers
{
    public static partial class ClientSkillHandler
    {
        private static SkillHandlerBase[] handlers;

        public static bool SkillTakesWeaponSound(CharacterSkill skill) => handlers[(int)skill].DoesAttackTakeWeaponSound; 

        public static void StartCastingSkill(ServerControllable src, ServerControllable target, CharacterSkill skillId, int lvl, float castTime) =>
            handlers[(int)skillId].StartSkillCasting(src, target, lvl, castTime);
        
        public static void StartCastingSkill(ServerControllable src, Vector2Int target, CharacterSkill skillId, int lvl, float castTime) =>
            handlers[(int)skillId].StartSkillCasting(src, target, lvl, castTime);
        
        public static void OnHitEffect(ServerControllable target, ref AttackResultData attack) =>
            handlers[(int)attack.Skill].OnHitEffect(target, ref attack);

        public static void ExecuteSkill(ServerControllable src, ref AttackResultData attack)
        {
            var skillId = attack.Skill;
            var handler = handlers[(int)skillId];
            
            // Debug.Log($"Execute skill {attack.Skill}");

            if (src == null && !handler.ExecuteWithoutSource)
                return;
            
            var targetType = ClientDataLoader.Instance.GetSkillTarget(skillId);

            if(targetType == SkillTarget.Ground)
                handler.ExecuteSkillGroundTargeted(src, ref attack); //need to fix this whole thing to have the target position
            else
                handler.ExecuteSkillTargeted(src, ref attack);
        }
    }
}