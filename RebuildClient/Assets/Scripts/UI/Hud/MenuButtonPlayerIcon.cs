using Assets.Scripts.PlayerControl;
using Assets.Scripts.Sprites;
using RebuildSharedData.Enum;
using UnityEngine;

namespace Assets.Scripts.UI.Hud
{
    //Draws the local player (sitting) as the menu opener button's icon, following PlayerState.AppearanceChanged.
    public class MenuButtonPlayerIcon : MonoBehaviour
    {
        public UiPlayerSprite PlayerSprite;

        public void OnEnable()
        {
            PlayerState.Instance.AppearanceChanged += Refresh;
            Refresh();
        }

        public void OnDisable()
        {
            PlayerState.Instance.AppearanceChanged -= Refresh;
        }

        public void Refresh()
        {
            var state = PlayerState.Instance;
            //the player and game data must exist - OnEnable can run at scene load before either is ready
            if (PlayerSprite == null || state == null || !state.IsValid || ClientDataLoader.Instance?.IsInitialized != true)
                return;

            PlayerSprite.PrepareDisplayPlayerCharacter(state.JobId, state.HairStyleId, state.HairColorId,
                state.Headgear1, state.Headgear2, state.Headgear3, state.IsMale);
            PlayerSprite.SetMotion(SpriteMotion.Sit);
        }
    }
}
