using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.UI.ConfigWindow
{
    [RequireComponent(typeof(Slider))]
    public sealed class SliderOption : MonoBehaviour, IPointerUpHandler
    {
        [SerializeField] private Slider slider;
        [SerializeField] private RangeOption option;
        [SerializeField] private bool applyOnRelease; // apply on pointer-up instead of live, for expensive applies (e.g. UI scale)

        private void Reset() => slider = GetComponent<Slider>();

        private void OnEnable()
        {
            SyncFromConfig();
            slider.onValueChanged.AddListener(OnChanged);
        }

        private void OnDisable() => slider.onValueChanged.RemoveListener(OnChanged);

        private void OnChanged(float value) => GameConfig.Set(option, value, apply: !applyOnRelease);

        public void SyncFromConfig() => slider.SetValueWithoutNotify(GameConfig.Get(option));

        public void Bind(RangeOption rangeOption, float min, float max, bool wholeNumbers, bool applyOnRelease)
        {
            option = rangeOption;
            this.applyOnRelease = applyOnRelease;
            if (slider == null) slider = GetComponent<Slider>();

            // Setting min/max re-clamps the value and fires onValueChanged; with the listener attached that
            // would write the minimum back into config. Detach while setting the range, then re-sync.
            slider.onValueChanged.RemoveListener(OnChanged);
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;
            SyncFromConfig();
            if (isActiveAndEnabled)
                slider.onValueChanged.AddListener(OnChanged);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (applyOnRelease)
                GameConfig.Apply(option);
        }
    }
}
