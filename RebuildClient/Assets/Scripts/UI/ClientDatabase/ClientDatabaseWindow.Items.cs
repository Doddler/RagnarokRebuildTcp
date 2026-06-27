using System;
using System.Collections.Generic;
using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI.Utility;
using Assets.Scripts.Utility;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Enum;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Scripts.UI.ClientDatabase
{
    public partial class ClientDatabaseWindow
    {
        private readonly List<ItemData> filteredItems = new();
        private int currentItemDetailId = -1;

        [SerializeField, HideInInspector] internal TextMeshProUGUI itemSearchGhost;
        [SerializeField, HideInInspector] internal TextMeshProUGUI itemDetailDescText;
        [SerializeField, HideInInspector] internal Image itemDetailIcon;

        private TmpLinkHover detailLinkHover;

        private void PopulateItemList()
        {
            itemPage.Refresh();
        }

        private void BindItemListRow(DatabaseListRow row, ItemData item, int index)
        {
            row.SetLabel(FormatItemName(item));
            row.SetIcon(GetItemIcon(item.Sprite));
            row.SetActions(
                () => ShowItemDetail(item),
                () => NetworkManager.Instance.SendAdminCreateItem(item.Id, 1));
        }

        public void OpenAndJumpToItem(int itemId)
        {
            ShowWindow();
            MoveToTop();
            if (!ItemLookup.TryGetValue(itemId, out var item))
                return;
            JumpToItem(item);
        }

        private bool TryHandleItemDetailLinkClick(PointerEventData eventData)
        {
            if (itemDetailDescText == null || !itemDetailDescText.gameObject.activeInHierarchy)
                return false;
            var idx = TMP_TextUtilities.FindIntersectingLink(itemDetailDescText, eventData.position, null);
            if (idx < 0) return false;
            var linkId = itemDetailDescText.textInfo.linkInfo[idx].GetLinkID();
            if (!linkId.StartsWith("item:", StringComparison.Ordinal)) return false;
            if (!int.TryParse(linkId.AsSpan(5), out var itemId)) return false;
            if (!ItemLookup.TryGetValue(itemId, out var item)) return false;
            JumpToItem(item);
            return true;
        }

        private void ShowItemDetail(ItemData item)
        {
            itemPage.ShowDetail();
            itemPage.DetailTitleText.text = FormatNameWithBracket(FormatItemName(item), item.Id.ToString());

            currentItemDetailId = item.Id;
            SetItemPortraitImmediate(null);
            LoadItemPortrait(item);

            var desc = "<i>No description.</i>";
            if (DescLookup.TryGetValue(item.Code, out var raw))
                desc = SanitizeRichText(raw);
            itemDetailDescText.text = desc + BuildItemInfoFooter(item);

            if (detailLinkHover == null && itemDetailDescText != null)
                detailLinkHover = itemDetailDescText.GetComponent<TmpLinkHover>();
            if (detailLinkHover != null)
                detailLinkHover.RefreshSource();

            ReleaseDetailRows(itemPage.SecondaryContent);
            if (!dropMap.TryGetValue(item.Id, out var droppers) || droppers.Count == 0)
            {
                CreateInfoRow(itemPage.SecondaryContent, "(not dropped by any monster)");
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

            ReleaseDetailRows(itemPage.TertiaryContent);
            if (item.SoldBy == null || item.SoldBy.Count == 0)
            {
                CreateInfoRow(itemPage.TertiaryContent, "(not sold by any NPC)");
            }
            else
            {
                foreach (var npcId in item.SoldBy)
                    CreateSoldByRow(npcId);
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

            var row = GetDetailRow(rowTemplate, itemPage.SecondaryContent, 24, clickable: hasMonster);
            row.GetComponentInChildren<TextMeshProUGUI>(true).text = $"{db.MonsterName}  —  {pct:0.##}%";
            if (hasMonster)
            {
                var captured = mon;
                row.GetComponent<Button>().onClick.AddListener(() => JumpToMonster(captured));
                AttachRightClick(row, () => NetworkManager.Instance.SendAdminSummonMonster(captured.Code, 1, false));
            }
        }

        private void CreateSoldByRow(int npcId)
        {
            var hasNpc = npcLookup.TryGetValue(npcId, out var npc);
            var label = hasNpc
                ? $"{npc.Name}  <size=80%><color=#888888>({FormatMapLabel(npc.Map, withBracket: false)})</color></size>"
                : $"NPC#{npcId}";

            var row = GetDetailRow(rowTemplate, itemPage.TertiaryContent, 24, clickable: hasNpc);
            row.GetComponentInChildren<TextMeshProUGUI>(true).text = label;
            if (hasNpc)
            {
                var captured = npc;
                row.GetComponent<Button>().onClick.AddListener(() => JumpToNpc(captured));
                AttachRightClick(row, () => NetworkManager.Instance.SendMoveRequest(captured.Map, captured.X, captured.Y));
            }
        }

        public void ReturnToItemList()
        {
            itemPage.ShowList();
        }

        private static readonly PredicateRegistry<ItemData> ItemPredicates = BuildItemPredicates();

        private static PredicateRegistry<ItemData> BuildItemPredicates()
        {
            var reg = BuildPredicateRegistry<ItemData>();
            reg.Register("description", (it, p) =>
                CompareStr(DescLookup.GetValueOrDefault(it.Code, ""), p));
            return reg;
        }

        private void FilterItems(string query) =>
            ApplyDatabaseFilter<ItemData, DatabaseListRow>(
                ItemLookup.Values,
                filteredItems,
                query,
                itemPage.VirtualList,
                nameof(itemPage),
                "Items",
                itemPage.TitleText,
                BindItemListRow,
                static item => $"{item.Id} {item.Name} {item.Code}",
                ItemPredicates,
                static item => item.Id >= 0,
                static (left, right) => left.Id.CompareTo(right.Id));
    }
}
