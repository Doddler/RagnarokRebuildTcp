using TMPro;
using UnityEngine;

namespace Assets.Scripts.UI.ConfigWindow
{
    [RequireComponent(typeof(TMP_InputField))]
    public sealed class NumberOption : MonoBehaviour
    {
        [SerializeField] private TMP_InputField input;
        [SerializeField] private RangeOption option;

        private void Reset() => input = GetComponent<TMP_InputField>();

        private void OnEnable()
        {
            SyncFromConfig();
            input.onEndEdit.AddListener(OnEndEdit);
        }

        private void OnDisable() => input.onEndEdit.RemoveListener(OnEndEdit);

        //applied when editing ends
        private void OnEndEdit(string text)
        {
            if (int.TryParse(text, out var value))
            {
                var bounds = GameConfig.BoundsFor(option);
                GameConfig.Set(option, Mathf.Clamp(value, bounds.Min, bounds.Max));
            }

            SyncFromConfig();
        }

        public void SyncFromConfig() => input.SetTextWithoutNotify(Mathf.RoundToInt(GameConfig.Get(option)).ToString());

        public void Bind(RangeOption rangeOption)
        {
            option = rangeOption;
            if (isActiveAndEnabled) SyncFromConfig();
        }
    }
}
