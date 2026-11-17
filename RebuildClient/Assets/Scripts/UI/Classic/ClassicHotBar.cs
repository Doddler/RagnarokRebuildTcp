using Assets.Scripts.UI.ConfigWindow;
using RO_Flex_UI.Components;
using RO_Flex_UI.Panels;
using UnityEngine;

namespace Assets.Scripts.UI.Classic
{
    public class ClassicHotBar : Window, IStyledWindow
    {
        public bool IsVisible => gameObject.activeSelf;

        [SerializeField] private IconAmount slotPrefab;
        [SerializeField] private Transform slotContainer;
        [SerializeField] private int containerCount;
        [SerializeField] private int slotCount;
        private Resizable resizable;
        private HotBarPanel[] lines;

        protected override void Awake()
        {
            base.Awake();
            UiManagerV2.Instance.RegisterWindow(WindowID.HOTBAR, this, UiStyle.Classic);
            slotPrefab.gameObject.SetActive(false);
            slotContainer.gameObject.SetActive(false);
        }

        public override void ShowWindow()
        {
            base.ShowWindow();
            UiManagerV2.Instance?.TrackOpen(this);
        }

        public override void HideWindow()
        {
            UiManagerV2.Instance?.TrackClosed(this);
            base.HideWindow();
        }

        protected void Start()
        {
            lines = new HotBarPanel[containerCount];
            for (var i = 0; i < containerCount; i++)
            {
                var line = Instantiate(slotContainer, transform);
                line.gameObject.SetActive(true);
                line.transform.SetAsFirstSibling();
                lines[containerCount - i - 1] = line.GetComponent<HotBarPanel>();
                lines[containerCount - i - 1].Initialize(containerCount - i, slotCount, slotPrefab, line);
            }

            resizable = GetComponentInChildren<Resizable>();
            var cellSize = 32;
            resizable.MinSize = new Vector2(slotCount * cellSize, cellSize + 2);
            resizable.MaxSize = new Vector2(slotCount * cellSize, containerCount * (cellSize + 2));
            resizable.StepSize = new Vector2(cellSize, cellSize + 2);

            // TODO initial size must load from saved settings or default to one row
            (transform as RectTransform).sizeDelta = new Vector2(slotCount * cellSize, cellSize + 2);
        }
    }
}
