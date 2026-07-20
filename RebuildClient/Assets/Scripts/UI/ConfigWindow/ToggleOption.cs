using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.ConfigWindow
{
    [RequireComponent(typeof(Toggle))]
    public sealed class ToggleOption : MonoBehaviour
    {
        [SerializeField] private Toggle toggle;
        [SerializeField] private BoolOption option;

        private void Reset() => toggle = GetComponent<Toggle>();

        private void OnEnable()
        {
            SyncFromConfig();
            toggle.onValueChanged.AddListener(OnChanged);
        }

        private void OnDisable() => toggle.onValueChanged.RemoveListener(OnChanged);

        private void OnChanged(bool value) => GameConfig.Set(option, value);

        public void SyncFromConfig() => toggle.SetIsOnWithoutNotify(GameConfig.Get(option));

        public void Bind(BoolOption boolOption)
        {
            option = boolOption;
            if (toggle == null) toggle = GetComponent<Toggle>();
            if (isActiveAndEnabled) SyncFromConfig();
        }
    }
}
