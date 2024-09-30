using UnityEngine;
using UnityEngine.EventSystems;

namespace Assets.Scripts.UI.TitleScreen
{
    public class CharacterCreatorWindow : UIBehaviour
    {
        public TitleScreen Parent;
        public GameObject Pane;

        public void HidePane()
        {
            Pane.SetActive(false);
        }
    }
}