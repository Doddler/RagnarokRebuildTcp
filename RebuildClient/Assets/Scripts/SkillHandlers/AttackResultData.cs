using Assets.Scripts.Network;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.SkillHandlers
{
    public struct AttackResultData
    {
        public CharacterSkill Skill;
        public ServerControllable Src;
        public ServerControllable Target;
        public Vector2Int TargetAoE;
        public float MotionTime;
        public float DamageTiming;
        public int Damage;
        public AttackResult Result;
        public byte HitCount;
        public byte SkillLevel;
    }
}