using System.Collections.Generic;
using Assets.Scripts.UI.Utility;
using UnityEngine;

namespace Assets.Scripts.UI.ConfigWindow
{
    public enum OptionSet { Full, TitleScreenMini }

    public sealed class OptionList : MonoBehaviour
    {
        [SerializeField] private OptionSet optionSet;
        [SerializeField] private float labelWidth = 280; // the control fills the rest of the row
        [SerializeField] private RectTransform content;
        [SerializeField] private TabGroupVisual tabGroup; // no tab group means every row stays visible
        [SerializeField] private OptionCategory[] tabCategories; // one entry per tab, in tab order
        [SerializeField] private OptionRowInfo headerRowPrefab;
        [SerializeField] private OptionRowInfo toggleRowPrefab;
        [SerializeField] private OptionRowInfo sliderRowPrefab;
        [SerializeField] private OptionRowInfo soundRowPrefab;
        [SerializeField] private OptionRowInfo toggleNumberRowPrefab;
        [SerializeField] private OptionRowInfo listRowPrefab;

        private OptionRowInfo[] rows;

        private OptionRow[] Rows => optionSet == OptionSet.Full ? GameConfig.Layout : GameConfig.TitleScreenLayout;

        private void Awake()
        {
            if (content == null)
                content = (RectTransform)transform;
            Build();
        }

        private void OnEnable()
        {
            if (tabGroup == null)
                return;

            tabGroup.OnTabSelected.AddListener(ShowForTab);
            if (tabGroup.SelectedTabIndex >= 0)
                ShowForTab(tabGroup.SelectedTabIndex);
        }

        private void OnDisable()
        {
            if (tabGroup != null)
                tabGroup.OnTabSelected.RemoveListener(ShowForTab);
        }

        private void Build()
        {
            // Rows read config the moment they are instantiated (OnEnable), so make sure it's loaded first.
            GameConfig.InitializeIfNecessary();

            var built = new List<OptionRowInfo>(Rows.Length);

            foreach (var row in Rows)
            {
                var prefab = PrefabFor(row.Kind);
                if (prefab == null)
                    continue;

                var info = Instantiate(prefab, content, false);
                info.Bind(row.Category);
                info.SetLabel(row.Label);
                if (row.Kind != OptionKind.Header)
                    info.SetLabelWidth(labelWidth);

                if (row.Kind is OptionKind.Slider or OptionKind.Sound)
                    info.GetComponentInChildren<SliderOption>(true)?.Bind(row.Range, row.WholeNumbers, row.ApplyOnRelease);
                if (row.Kind is OptionKind.Toggle or OptionKind.Sound or OptionKind.ToggleNumber)
                    info.GetComponentInChildren<ToggleOption>(true)?.Bind(row.Bool);
                if (row.Kind is OptionKind.ToggleNumber)
                    info.GetComponentInChildren<NumberOption>(true)?.Bind(row.Range);
                if (row.Kind is OptionKind.List)
                    info.GetComponentInChildren<DropdownOption>(true)?.Bind(row.List);

                built.Add(info);
            }

            rows = built.ToArray();
        }

        private void ShowForTab(int tabIndex)
        {
            if (tabIndex < 0 || tabIndex >= tabCategories.Length)
                return;

            var category = tabCategories[tabIndex];
            foreach (var row in rows)
                row.gameObject.SetActive(row.Category == category);
        }

        private OptionRowInfo PrefabFor(OptionKind kind) => kind switch
        {
            OptionKind.Header => headerRowPrefab,
            OptionKind.Toggle => toggleRowPrefab,
            OptionKind.Slider => sliderRowPrefab,
            OptionKind.Sound => soundRowPrefab,
            OptionKind.ToggleNumber => toggleNumberRowPrefab,
            OptionKind.List => listRowPrefab,
            _ => null
        };
    }
}
