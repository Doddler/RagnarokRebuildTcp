using Assets.Scripts.UI.Utility;
using UnityEngine;

namespace Assets.Scripts.UI.ConfigWindow
{
    public sealed class OptionList : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private TabGroupVisual tabGroup;
        [SerializeField] private OptionCategory[] tabCategories; // one entry per tab, in tab order
        [SerializeField] private GameObject headerRowPrefab;
        [SerializeField] private GameObject toggleRowPrefab;
        [SerializeField] private GameObject sliderRowPrefab;
        [SerializeField] private GameObject soundRowPrefab;

        private OptionRowInfo[] rows;

        private void Awake()
        {
            if (content == null)
                content = (RectTransform)transform;
            Build();
        }

        private void OnEnable()
        {
            tabGroup.OnTabSelected.AddListener(ShowForTab);
            if (tabGroup.SelectedTabIndex >= 0)
                ShowForTab(tabGroup.SelectedTabIndex);
        }

        private void OnDisable() => tabGroup.OnTabSelected.RemoveListener(ShowForTab);

        private void Build()
        {
            // Rows read config the moment they are instantiated (OnEnable), so make sure it's loaded first.
            GameConfig.InitializeIfNecessary();

            foreach (var row in GameConfig.Layout)
            {
                var prefab = PrefabFor(row.Kind);
                if (prefab == null)
                    continue;

                var go = Instantiate(prefab, content, false);
                var info = go.GetComponent<OptionRowInfo>();
                info.Bind(row.Category);
                info.SetLabel(row.Label);

                if (row.Kind is OptionKind.Slider or OptionKind.Sound)
                    go.GetComponentInChildren<SliderOption>(true)?.Bind(row.Range, row.Min, row.Max, row.WholeNumbers, row.ApplyOnRelease);
                if (row.Kind is OptionKind.Toggle or OptionKind.Sound)
                    go.GetComponentInChildren<ToggleOption>(true)?.Bind(row.Bool);
            }

            rows = content.GetComponentsInChildren<OptionRowInfo>(true);
        }

        private void ShowForTab(int tabIndex)
        {
            if (rows == null || tabIndex < 0 || tabIndex >= tabCategories.Length)
                return;

            var category = tabCategories[tabIndex];
            foreach (var row in rows)
                row.gameObject.SetActive(row.Category == category);
        }

        private GameObject PrefabFor(OptionKind kind) => kind switch
        {
            OptionKind.Header => headerRowPrefab,
            OptionKind.Toggle => toggleRowPrefab,
            OptionKind.Slider => sliderRowPrefab,
            OptionKind.Sound => soundRowPrefab,
            _ => null
        };
    }
}
