using RO_Flex_UI.Components;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.Classic
{
    public class UIScaler : MonoBehaviour
    {
        public CanvasScaler scaler;
        public RoSlider slider;

        public void Start()
        {
            if (scaler == null) return;
            if (slider == null) return;

            slider.onPointerUp.AddListener(value => scaler.scaleFactor = value);
            slider.onIncreaseClick.AddListener((value) => scaler.scaleFactor = value);
            slider.onDecreaseClick.AddListener((value) => scaler.scaleFactor = value);
        }
    }
}