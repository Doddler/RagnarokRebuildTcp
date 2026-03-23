using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.PlayerControl
{
    public interface IEntityActionHandler
    {
        void ChangeCharacterState(CharacterState state) {}
        void SetOrientation(FacingDirection facing) {}
        void LookTowards(Vector3 target) {}
        
    }
}