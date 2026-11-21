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
            if (PlayerSprite == null || state == null || !state.IsReadyForDisplay)
                return;

            PlayerSprite.PrepareDisplayPlayerCharacter(state.Appearance);
            PlayerSprite.SetMotion(SpriteMotion.Sit);
        }
    }
}
