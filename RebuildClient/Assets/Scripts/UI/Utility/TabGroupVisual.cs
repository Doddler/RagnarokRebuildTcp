using System;
using UnityEngine;
using UnityEngine.Events;

namespace Assets.Scripts.UI.Utility
{
    public class TabGroupVisual : MonoBehaviour
    {
        private enum LayoutDirection
        {
            Vertical,
            Horizontal
        }

        [SerializeField] private TabButtonVisual[] tabs;
        [SerializeField] private int defaultSelectedTab;
        [SerializeField] private bool selectedTabDrawsOnTop = true;
        [SerializeField] private bool controlTabLayout;
        [SerializeField] private LayoutDirection layoutDirection = LayoutDirection.Vertical;
        [SerializeField] private Vector2 tabSize = new(60, 72);
        [SerializeField] private float spacing;
        [SerializeField] private float firstTabTopOffset = 5f;
        [SerializeField] private float selectedTabRightOffset = 2.5f;
        [SerializeField] private float selectedExtraWidth;
        [SerializeField] private float selectedExtraHeight;
        [SerializeField] private Sprite tabUnselectedSprite;
        [SerializeField] private Sprite tabSelectedSprite;
        [SerializeField] private Material unselectedMaterial;
        [SerializeField] private Material hoveredMaterial;

        public UnityEvent<int> OnTabSelected = new();

        private readonly Vector3[] tabCorners = new Vector3[4];
        private int selectedTabIndex = -1;

        public int TabCount => tabs.Length;
        public int SelectedTabIndex => selectedTabIndex;

        public Sprite GetTabSprite(bool selected)
        {
            return selected ? tabSelectedSprite : tabUnselectedSprite;
        }

        public Material GetIconMaterial(bool selected, bool hovered)
        {
            if (selected)
                return null;

            return hovered ? hoveredMaterial : unselectedMaterial;
        }

        public float GetLeftEdge(float fallback)
        {
            if (selectedTabIndex < 0 || selectedTabIndex >= tabs.Length ||
                !tabs[selectedTabIndex].gameObject.activeInHierarchy)
                return fallback;

            ((RectTransform)tabs[selectedTabIndex].transform).GetWorldCorners(tabCorners);
            return Mathf.Min(fallback, tabCorners[0].x);
        }

        private void Awake()
        {
            InitializeTabs();
        }

        private void OnEnable()
        {
            if (selectedTabIndex < 0)
                SelectTab(Mathf.Clamp(defaultSelectedTab, 0, tabs.Length - 1));
        }

        private void OnValidate()
        {
            if (tabs == null || tabs.Length == 0)
                return;

            selectedTabIndex = Mathf.Clamp(defaultSelectedTab, 0, tabs.Length - 1);
            LayoutTabs();
            UpdateTabSelection();
        }

        public void SelectTab(int index, bool forceRefresh = false)
        {
            if (index < 0 || index >= tabs.Length)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Tab index is outside the tab array.");

            if (!forceRefresh && index == selectedTabIndex)
                return;

            selectedTabIndex = index;
            LayoutTabs();
            UpdateTabSelection();

            if (selectedTabDrawsOnTop)
                BringSelectedTabToTop(index);

            OnTabSelected.Invoke(index);
        }

        public void SelectTab(TabButtonVisual selectedTab)
        {
            var index = Array.IndexOf(tabs, selectedTab);
            if (index >= 0)
                SelectTab(index);
        }

        public void SetTabActive(int index, bool active)
        {
            tabs[index].gameObject.SetActive(active);
            LayoutTabs();
        }

        public void SetTabIcon(int index, Sprite icon)
        {
            if (index < 0 || index >= tabs.Length)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Tab index is outside the tab array.");

            tabs[index].SetIcon(icon);
        }

        public void SetTabIconFromItemAtlas(int index, string spriteName)
        {
            if (index < 0 || index >= tabs.Length)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Tab index is outside the tab array.");

            tabs[index].SetIconFromItemAtlas(spriteName);
        }

        public void SetTabIconFromRoSprite(int index, string spriteAddress, string embeddedSpriteName = null)
        {
            if (index < 0 || index >= tabs.Length)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Tab index is outside the tab array.");

            tabs[index].SetIconFromRoSprite(spriteAddress, embeddedSpriteName);
        }

        private void InitializeTabs()
        {
            for (var i = 0; i < tabs.Length; i++)
            {
                tabs[i].Initialize(this);
            }

            LayoutTabs();
        }

        private void UpdateTabSelection()
        {
            for (var i = 0; i < tabs.Length; i++)
            {
                tabs[i].SetSelected(i == selectedTabIndex, this);
            }
        }

        private void LayoutTabs()
        {
            if (!controlTabLayout)
                return;

            var visibleIndex = 0;
            for (var i = 0; i < tabs.Length; i++)
            {
                if (!tabs[i].gameObject.activeSelf)
                    continue;

                var rect = tabs[i].transform as RectTransform;

                var position = GetFirstTabPosition();
                if (layoutDirection == LayoutDirection.Vertical)
                    position += new Vector2(0, -visibleIndex * (tabSize.y + spacing));
                else
                    position += new Vector2(visibleIndex * (tabSize.x + spacing), 0);

                if (i == selectedTabIndex)
                    position.x += GetSelectedXOffset();

                rect.sizeDelta = tabSize + (i == selectedTabIndex ? new Vector2(selectedExtraWidth, selectedExtraHeight) : Vector2.zero);
                rect.anchoredPosition = position;
                visibleIndex++;
            }
        }

        private Vector2 GetFirstTabPosition()
        {
            return new Vector2(-tabSize.x / 2f, firstTabTopOffset - (tabSize.y + selectedExtraHeight) / 2f);
        }

        private float GetSelectedXOffset()
        {
            return selectedTabRightOffset - selectedExtraWidth / 2f;
        }

        private void BringSelectedTabToTop(int index)
        {
            for (var i = tabs.Length - 1; i >= 0; i--)
            {
                if (i == index)
                    continue;

                tabs[i].transform.SetAsLastSibling();
            }

            tabs[index].transform.SetAsLastSibling();
        }
    }
}
