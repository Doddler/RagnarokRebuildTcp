using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using Assets.Scripts.Utility;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Enum;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.ClientDatabase
{
    public partial class ClientDatabaseWindow
    {
        private readonly List<(GameObject go, ItemData entry, string searchText)> itemRowEntries = new();
        private int currentItemDetailId = -1;

        [SerializeField, HideInInspector] internal GameObject itemsContainer;
        [SerializeField, HideInInspector] internal Image itemsTabImage;
        [SerializeField, HideInInspector] internal Button itemBackButton;
        [SerializeField, HideInInspector] internal TMP_InputField itemSearchField;
        [SerializeField, HideInInspector] internal TextMeshProUGUI itemSearchGhost;
        [SerializeField, HideInInspector] internal GameObject itemListView;
        [SerializeField, HideInInspector] internal GameObject itemDetailView;
        [SerializeField, HideInInspector] internal TextMeshProUGUI itemListTitleText;
        [SerializeField, HideInInspector] internal GameObject itemListContent;
        [SerializeField, HideInInspector] internal TextMeshProUGUI itemDetailNameText;
        [SerializeField, HideInInspector] internal TextMeshProUGUI itemDetailDescText;
        [SerializeField, HideInInspector] internal Image itemDetailIcon;
        [SerializeField, HideInInspector] internal GameObject droppedByContent;
        
        private void PopulateItemList()
        {
            if (itemListContent == null) return;
            ClearChildren(itemListContent.transform);
            itemRowEntries.Clear();

            var items = ItemLookup;
            if (items.Count == 0)
            {
                if (itemListTitleText != null) itemListTitleText.text = "Items";
                return;
            }

            var ordered = items.Values.Where(it => it.Id >= 0).OrderBy(it => it.Id).ToList();
            if (itemListTitleText != null) itemListTitleText.text = $"Items ({ordered.Count})";
            StartCoroutine(LoadItemsAsync(ordered));
        }

        private IEnumerator LoadItemsAsync(List<ItemData> ordered)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            foreach (var item in ordered)
            {
                AddItemListRow(item);
                if (sw.Elapsed.TotalMilliseconds >= 12f) { yield return null; sw.Restart(); }
            }
            itemsLoaded = true;
        }

        private bool itemsLoaded;

        private void AddItemListRow(ItemData item)
        {
            var row = CloneRow(iconRowTemplate, itemListContent.transform, 28);
            row.GetComponentInChildren<TextMeshProUGUI>(true).text = FormatItemName(item);

            var iconImage = row.transform.Find("Icon").GetComponent<Image>();
            var sprite = GetItemIcon(item.Sprite);
            iconImage.sprite = sprite;
            iconImage.color = sprite != null ? Color.white : new Color(1, 1, 1, 0.08f);

            var captured = item;
            row.GetComponent<Button>().onClick.AddListener(() => ShowItemDetail(captured));
            AttachRightClick(row, () => NetworkManager.Instance.SendAdminCreateItem(captured.Id, 1));
            itemRowEntries.Add((row, item, $"{item.Id} {item.Name} {item.Code}"));
        }

        private void ShowItemDetail(ItemData item)
        {
            itemListView.SetActive(false);
            itemDetailView.SetActive(true);
            itemDetailNameText.text = FormatNameWithBracket(FormatItemName(item), item.Id.ToString());

            currentItemDetailId = item.Id;
            SetItemPortraitImmediate(null);
            LoadItemPortrait(item);

            var desc = "<i>No description.</i>";
            if (DescLookup.TryGetValue(item.Code, out var raw))
                desc = SanitizeRichText(raw);
            itemDetailDescText.text = desc + BuildItemInfoFooter(item);

            ClearChildren(droppedByContent.transform);
            if (!dropMap.TryGetValue(item.Id, out var droppers) || droppers.Count == 0)
            {
                CreateInfoRow(droppedByContent.transform, "(not dropped by any monster)");
            }
            else
            {
                if (sortedDropMapKeys.Add(item.Id))
                    droppers.Sort(static (a, b) => b.Chance.CompareTo(a.Chance));
                foreach (var db in droppers)
                {
                    CreateDroppedByRow(db);
                }
            }
        }

        private readonly HashSet<int> sortedDropMapKeys = new();

        private static string BuildItemInfoFooter(ItemData item)
        {
            if (item.Price <= 0 && item.SellPrice <= 0) return "";
            var sb = new System.Text.StringBuilder();
            sb.Append("\n\n<size=85%><color=#666666>");
            if (item.Price > 0) sb.Append($"Buy {item.Price}z");
            if (item.Price > 0 && item.SellPrice > 0) sb.Append("  ·  ");
            if (item.SellPrice > 0) sb.Append($"Sell {item.SellPrice}z");
            sb.Append("</color></size>");
            return sb.ToString();
        }

        private void SetItemPortraitImmediate(Sprite sprite)
        {
            if (sprite != null)
            {
                itemDetailIcon.sprite = sprite;
                itemDetailIcon.color = Color.white;
            }
            else
            {
                itemDetailIcon.sprite = null;
                itemDetailIcon.color = new Color(1, 1, 1, 0.08f);
            }
        }
        
        private void LoadItemPortrait(ItemData item)
        {
            if (string.IsNullOrEmpty(item.Sprite)) return;

            var path = $"Assets/Sprites/Imported/Collections/{item.Sprite}.png";
            if (ClientDataLoader.DoesAddressableExist<Sprite>(path))
            {
                var requestedId = item.Id;
                AddressableUtility.LoadSprite(gameObject, path, loaded =>
                {
                    if (loaded == null || currentItemDetailId != requestedId) return;
                    SetItemPortraitImmediate(loaded);
                });
                return;
            }

            SetItemPortraitImmediate(GetItemIcon(item.Sprite));
        }

        private void CreateDroppedByRow(DroppedBy db)
        {
            var hasMonster = monsterLookup.TryGetValue(db.MonsterId, out var mon);
            var pct = db.Chance / 100f;

            var row = CloneRow(rowTemplate, droppedByContent.transform, 24, clickable: hasMonster);
            row.GetComponentInChildren<TextMeshProUGUI>(true).text = $"{db.MonsterName}  —  {pct:0.##}%";
            if (hasMonster)
            {
                var captured = mon;
                row.GetComponent<Button>().onClick.AddListener(() => JumpToMonster(captured));
                AttachRightClick(row, () => NetworkManager.Instance.SendAdminSummonMonster(captured.Code, 1, false));
            }
        }

        private void ReturnToItemList()
        {
            if (itemListView != null) itemListView.SetActive(true);
            if (itemDetailView != null) itemDetailView.SetActive(false);
        }

        private static readonly PredicateRegistry<ItemData> ItemPredicates = BuildItemPredicates();

        private static PredicateRegistry<ItemData> BuildItemPredicates()
        {
            var reg = BuildPredicateRegistry<ItemData>();
            reg.Register("description", (it, p) =>
                CompareStr(DescLookup.GetValueOrDefault(it.Code, ""), p));
            return reg;
        }

        private void FilterItems(string query) => ApplyFilter(itemRowEntries, query, (it, p) => ItemPredicates.TryMatch(it, p, out var r) && r);
    }
}
