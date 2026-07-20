using System;
using Assets.Scripts.UI.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.ClientDatabase
{
    [DisallowMultipleComponent]
    public sealed class ClientDatabasePage : MonoBehaviour
    {
        private const float FilterDebounceSeconds = 0.1f;

        [SerializeField] private GameObject listView;
        [SerializeField] private GameObject detailView;
        [SerializeField] private TMP_InputField searchField;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private VirtualList virtualList;
        [SerializeField] private Button backButton;
        [SerializeField] private TextMeshProUGUI detailTitleText;
        [SerializeField] private RectTransform detailContent;
        [SerializeField] private RectTransform visualSlot;
        [SerializeField] private TextMeshProUGUI primaryTitle;
        [SerializeField] private RectTransform primaryScroll;
        [SerializeField] private RectTransform primaryContent;
        [SerializeField] private RectTransform secondarySection;
        [SerializeField] private TextMeshProUGUI secondaryTitle;
        [SerializeField] private RectTransform secondaryScroll;
        [SerializeField] private RectTransform secondaryContent;
        [SerializeField] private RectTransform tertiarySection;
        [SerializeField] private TextMeshProUGUI tertiaryTitle;
        [SerializeField] private RectTransform tertiaryScroll;
        [SerializeField] private RectTransform tertiaryContent;
        private Action<string> applyFilter;
        private string pendingQuery;
        private bool initialized;

        public string Query => searchField.text;
        public TMP_InputField SearchField => searchField;
        public TextMeshProUGUI TitleText => titleText;
        public VirtualList VirtualList => virtualList;
        public TextMeshProUGUI DetailTitleText => detailTitleText;
        public RectTransform DetailContent => detailContent;
        public RectTransform VisualSlot => visualSlot;
        public RectTransform PrimaryContent => primaryContent;
        public RectTransform SecondaryContent => secondaryContent;
        public RectTransform TertiaryContent => tertiaryContent;

        public void Initialize(
            string title,
            GameObject itemPrefab,
            Action<string> filter,
            string primarySectionTitle,
            string secondarySectionTitle,
            string tertiarySectionTitle)
        {
            if (initialized)
                return;

            applyFilter = filter;

            titleText.text = title ?? throw new ArgumentNullException(nameof(title));
            virtualList.SetItemPrefab(itemPrefab);
            ConfigureDetailSections(primarySectionTitle, secondarySectionTitle, tertiarySectionTitle);
            searchField.onValueChanged.AddListener(ScheduleFilter);
            backButton.onClick.AddListener(ShowList);
            initialized = true;
        }

        private void ConfigureDetailSections(
            string primarySectionTitle,
            string secondarySectionTitle,
            string tertiarySectionTitle)
        {
            ConfigureTitle(primaryTitle, primarySectionTitle);
            primaryScroll.gameObject.SetActive(true);
            ConfigureSection(secondarySection, secondaryTitle, secondaryScroll, secondarySectionTitle);
            ConfigureSection(tertiarySection, tertiaryTitle, tertiaryScroll, tertiarySectionTitle);
        }

        private static void ConfigureSection(
            RectTransform section,
            TextMeshProUGUI title,
            RectTransform scroll,
            string sectionTitle)
        {
            var active = !string.IsNullOrEmpty(sectionTitle);
            section.gameObject.SetActive(active);
            ConfigureTitle(title, sectionTitle);
            scroll.gameObject.SetActive(active);
        }

        private static void ConfigureTitle(TextMeshProUGUI title, string sectionTitle)
        {
            var active = !string.IsNullOrEmpty(sectionTitle);
            title.gameObject.SetActive(active);
            if (active)
                title.text = sectionTitle;
        }

        public void SetActive(bool active)
        {
            EnsureInitialized();
            gameObject.SetActive(active);
        }

        public void ShowList()
        {
            EnsureInitialized();
            listView.SetActive(true);
            detailView.SetActive(false);
        }

        public void ShowDetail()
        {
            EnsureInitialized();
            listView.SetActive(false);
            detailView.SetActive(true);
        }

        public void Refresh()
        {
            EnsureInitialized();
            applyFilter(Query);
        }

        public void ScheduleFilter(string query)
        {
            EnsureInitialized();
            pendingQuery = query ?? "";
            CancelInvoke(nameof(ApplyPendingFilter));
            Invoke(nameof(ApplyPendingFilter), FilterDebounceSeconds);
        }

        private void ApplyPendingFilter()
        {
            applyFilter(pendingQuery);
        }

        private void OnDisable()
        {
            CancelInvoke(nameof(ApplyPendingFilter));
        }

        private void OnDestroy()
        {
            if (initialized)
            {
                searchField.onValueChanged.RemoveListener(ScheduleFilter);
                backButton.onClick.RemoveListener(ShowList);
            }
        }

        private void EnsureInitialized()
        {
            if (!initialized)
                throw new InvalidOperationException($"{nameof(ClientDatabasePage)} on {name} has not been initialized.");
        }
    }
}
