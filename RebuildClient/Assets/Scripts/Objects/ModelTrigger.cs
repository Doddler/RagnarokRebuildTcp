using Assets.Scripts.PlayerControl;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.Objects
{
    public class ModelTrigger : MonoBehaviour, IEntityActionHandler
    {
        public RoKeyframeRotator[] Rotators;

        public void Activate()
        {
            if (Rotators == null)
                return;
            
            foreach (var r in Rotators)
            {
                r.enabled = true;
            }
        }

        public void ChangeCharacterState(CharacterState state)
        {
            if (state == CharacterState.Activated)
                Activate();
        }
    }
}