using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.PlayerControl
{
    public interface IEntityActionHandler
    {
        void ChangeCharacterState(CharacterState state) {}
        void SetAngle(float angle) {}
        void LookTowards(Vector3 target) {}
        void AttackMotion() {}
        void SetAttackMotionTime(float time) {} 
        void SetColor(Color color) {}
        void SetHide(bool isHidden) {}


    }
}