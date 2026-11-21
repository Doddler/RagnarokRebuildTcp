using TMPro;
using UnityEngine;

namespace Assets.Scripts.UI.ConfigWindow
{
    [RequireComponent(typeof(TMP_Dropdown))]
    public sealed class DropdownOption : MonoBehaviour
    {
        [SerializeField] private TMP_Dropdown dropdown;
        [SerializeField] private ListOption option;

        private void Reset() => dropdown = GetComponent<TMP_Dropdown>();

        private void OnEnable()
        {
            SyncFromConfig();
            dropdown.onValueChanged.AddListener(OnChanged);
        }

        private void OnDisable() => dropdown.onValueChanged.RemoveListener(OnChanged);

        private void OnChanged(int index) => GameConfig.ListFor(option).Set(index);

        //a value missing from the list shows the first choice
        public void SyncFromConfig()
        {
            var binding = GameConfig.ListFor(option);
            dropdown.ClearOptions();
            dropdown.AddOptions(binding.Labels());
            dropdown.SetValueWithoutNotify(Mathf.Max(binding.Get(), 0));
        }

        public void Bind(ListOption listOption)
        {
            option = listOption;
            if (isActiveAndEnabled) SyncFromConfig();
        }
    }
}
