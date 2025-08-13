using Assets.Scripts.Network;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Hud
{
    public class CharacterDetailBox : MonoBehaviour
    {
        public TextMeshProUGUI CharacterName;
        public TextMeshProUGUI CharacterJob;
        public TextMeshProUGUI CharacterZeny;
        public TextMeshProUGUI CharacterWeight;
        
        public TextMeshProUGUI HpDisplay;
        public Slider HpSlider;
        public TextMeshProUGUI SpDisplay;
        public Slider SpSlider;
        public TextMeshProUGUI ExpDisplay;
        public Slider ExpSlider;
        public TextMeshProUGUI JobExpDisplay;
        public Slider JobExpSlider;
        public TextMeshProUGUI BaseLvlDisplay;
        public TextMeshProUGUI JobLvlDisplay;
        public TextMeshProUGUI DebugInfo;

        public void UpdateWeightAndZeny()
        {
            var state = NetworkManager.Instance.PlayerState;
            
            var curWeight = state.CurrentWeight / 10;
            var totalWeight = state.MaxWeight / 10;
            // var weightPercent = curWeight * 100 / totalWeight;
            
            CameraFollower.Instance.CharacterDetailBox.CharacterWeight.text = $"Weight: <size=-1>{curWeight} / {totalWeight}";
            CameraFollower.Instance.CharacterDetailBox.CharacterZeny.text = $"Zeny:  <size=-1>{state.Zeny:N0}";
        }
    }
}