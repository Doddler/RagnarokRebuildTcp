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

        // The range comes from GameConfig.BoundsFor.
        private void ApplyBounds()
        {
            var bounds = GameConfig.BoundsFor(option);
            slider.minValue = bounds.Min;
            slider.maxValue = bounds.Max;
        }

        private void OnDisable() => slider.onValueChanged.RemoveListener(OnChanged);

        private void OnChanged(float value) => GameConfig.Set(option, value, apply: !applyOnRelease);

        public void SyncFromConfig() => slider.SetValueWithoutNotify(GameConfig.Get(option));

        public void Bind(RangeOption rangeOption, bool wholeNumbers, bool applyOnRelease)
        {
            option = rangeOption;
            this.applyOnRelease = applyOnRelease;

            // Setting the range fires onValueChanged, so detach to keep the clamped value out of config.
            slider.onValueChanged.RemoveListener(OnChanged);
            ApplyBounds();
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
