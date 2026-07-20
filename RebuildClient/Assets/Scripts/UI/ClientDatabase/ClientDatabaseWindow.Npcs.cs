using System;
using System.Collections.Generic;
using System.Text;
using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI.Utility;
using RebuildSharedData.ClientTypes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.ClientDatabase
{
    public partial class ClientDatabaseWindow
    {
        private const string NpcSpriteBasePath = "Assets/Sprites/Npcs/";
        private const int FirstMonsterClassId = 4000;

        private float npcFrameTimer;
        private int npcFrame;
        private int currentNpcDetailId = -1;
        private bool npcClassByCodeBuilt;

        private IReadOnlyList<NpcEntry> npcEntries = Array.Empty<NpcEntry>();
        private readonly List<NpcEntry> filteredNpcs = new();
        private readonly Dictionary<int, NpcEntry> npcLookup = new();
        private readonly Dictionary<string, List<NpcEntry>> mapNpcsLookup = new();
        private readonly Dictionary<string, MonsterClassData> npcClassByCode = new(StringComparer.OrdinalIgnoreCase);

        [Serializable]
        internal class NpcEntry
        {
            public int Id;
            public string Map;
            public string Name;
            public string SpriteCode;
            public int X;
            public int Y;
            public string Facing;
            public bool IsTrader;
            public List<int> SellsItems;
        }

        [Serializable] private class NpcDbFile { public List<NpcEntry> Items; }

        [SerializeField, HideInInspector] internal TextMeshProUGUI npcSearchGhost;
        [SerializeField, HideInInspector] internal TextMeshProUGUI npcDetailStatsText;
        [SerializeField, HideInInspector] internal UiPlayerSprite npcSprite;

        private NpcDbFile LoadNpcDatabase() => LoadStreamingJson<NpcDbFile>("ClientConfigGenerated/npcdatabase.json");

        private void BuildNpcReverseLookups(NpcDbFile npcDb)
        {
            if (npcDb?.Items == null) return;

            foreach (var npc in npcDb.Items)
            {
                npcLookup[npc.Id] = npc;
                if (!mapNpcsLookup.TryGetValue(npc.Map, out var list))
                    mapNpcsLookup[npc.Map] = list = new List<NpcEntry>();
                list.Add(npc);
            }
        }

        // ClientDataLoader populates MonsterClassLookup asynchronously (after Start runs),
        // so build the Code→class dict lazily the first time someone needs an NPC's sprite.
        // Use a flag instead of Count==0 so we don't keep rebuilding if no NPC classes are registered.
        private void EnsureNpcClassByCode()
        {
            if (npcClassByCodeBuilt) return;
            var classes = ClassLookup;
            if (classes.Count == 0) return;
            foreach (var cls in classes.Values)
            {
                if (cls.Id < FirstMonsterClassId && !string.IsNullOrEmpty(cls.Code))
                    npcClassByCode[cls.Code] = cls;
            }
            npcClassByCodeBuilt = true;
        }

        private void PopulateNpcList(NpcDbFile data)
        {
            npcEntries = data?.Items ?? (IReadOnlyList<NpcEntry>)Array.Empty<NpcEntry>();
            npcPage.Refresh();
        }

        private void BindNpcListRow(DatabaseListRow row, NpcEntry n, int index)
        {
            var mapLabel = FormatMapLabel(n.Map, withBracket: false);
            var label = $"{n.Name}  <size=80%><color=#888888>({mapLabel})</color></size>";

            row.SetLabel(label);
            row.SetIcon(null);
            row.SetActions(
                () => ShowNpcDetail(n),
                () => NetworkManager.Instance.SendMoveRequest(n.Map, n.X, n.Y));
        }

        private void ShowNpcDetail(NpcEntry n)
        {
            npcPage.ShowDetail();
            currentNpcDetailId = n.Id;
            npcPage.DetailTitleText.text = FormatNameWithBracket(n.Name, n.Id.ToString());

            var sb = new StringBuilder();
            sb.AppendLine($"<b>Map:</b> {FormatMapLabel(n.Map)}");
            sb.AppendLine($"<b>Position:</b> ({n.X}, {n.Y})");
            sb.AppendLine($"<b>Type:</b> {(n.IsTrader ? "Trader" : "NPC")}");
            npcDetailStatsText.text = sb.ToString();

            ReleaseDetailRows(npcPage.SecondaryContent);
            if (n.SellsItems == null || n.SellsItems.Count == 0)
            {
                CreateInfoRow(npcPage.SecondaryContent, "(this NPC does not sell anything)");
            }
            else
            {
                foreach (var itemId in n.SellsItems)
                    CreateNpcSellsRow(itemId);
            }

            LoadNpcSprite(n.SpriteCode);
        }

        private void CreateNpcSellsRow(int itemId)
        {
            var hasItem = ItemLookup.TryGetValue(itemId, out var item);
            var itemName = hasItem ? FormatItemName(item) : $"Item#{itemId}";
            var price = hasItem ? item.Price : 0;
            var label = price > 0 ? $"{itemName}  —  {price}z" : itemName;

            var row = GetDetailRow(rowTemplate, npcPage.SecondaryContent, 26, clickable: hasItem);
            row.GetComponentInChildren<TextMeshProUGUI>(true).text = label;
            SetRowIcon(row, hasItem ? GetItemIcon(item.Sprite) : null);

            if (hasItem)
            {
                var captured = item;
                row.GetComponent<Button>().onClick.AddListener(() => JumpToItem(captured));
                AttachRightClick(row, () => NetworkManager.Instance.SendAdminCreateItem(captured.Id, 1));
            }
        }

        private void LoadNpcSprite(string spriteCode)
        {
            npcSprite.Clear();
            npcSprite.gameObject.SetActive(false);
            npcFrame = 0;
            npcFrameTimer = 0f;

            if (string.IsNullOrEmpty(spriteCode)) return;
            EnsureNpcClassByCode();
            if (!npcClassByCode.TryGetValue(spriteCode, out var cls) || string.IsNullOrEmpty(cls.SpriteName))
                return;

            var path = NpcSpriteBasePath + cls.SpriteName;
            npcSprite.gameObject.SetActive(true);
            npcSprite.DisplaySprite(path);
        }

        private void JumpToNpc(NpcEntry npc)
        {
            tabGroup.SelectTab(2);
            ShowNpcDetail(npc);
        }

        public void ReturnToNpcList()
        {
            npcPage.ShowList();
            npcSprite.Clear();
            npcSprite.gameObject.SetActive(false);
            npcFrame = 0;
        }

        internal static readonly PredicateRegistry<NpcEntry> NpcPredicates = BuildPredicateRegistry<NpcEntry>();

        private void FilterNpcs(string query) =>
            ApplyDatabaseFilter<NpcEntry, DatabaseListRow>(
                npcEntries,
                filteredNpcs,
                query,
                npcPage.VirtualList,
                nameof(npcPage),
                "NPCs",
                npcPage.TitleText,
                BindNpcListRow,
                npc => $"{npc.Id} {npc.Name} {npc.Map} {FormatMapLabel(npc.Map, withBracket: false)} {npc.SpriteCode}",
                NpcPredicates,
                sort: static (left, right) =>
                {
                    var mapComparison = string.Compare(left.Map, right.Map, StringComparison.Ordinal);
                    return mapComparison != 0
                        ? mapComparison
                        : string.Compare(left.Name, right.Name, StringComparison.Ordinal);
                });
    }
}
