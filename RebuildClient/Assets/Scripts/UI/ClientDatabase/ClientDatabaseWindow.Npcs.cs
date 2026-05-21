using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        private const string NpcSpriteBasePath = "Assets/Sprites/Npcs/";
        private const float NpcSpriteFitSize = 160f;
        private const float NpcSpriteNaturalScale = 100f;
        private const int FirstMonsterClassId = 4000;

        private float npcFrameTimer;
        private int npcIdleFrameCount;
        private int currentNpcDetailId = -1;
        private bool npcClassByCodeBuilt;

        private readonly List<(GameObject go, NpcEntry entry, string searchText)> npcRowEntries = new();
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

        [SerializeField, HideInInspector] internal GameObject npcsContainer;
        [SerializeField, HideInInspector] internal Image npcsTabImage;
        [SerializeField, HideInInspector] internal Button npcBackButton;
        [SerializeField, HideInInspector] internal TMP_InputField npcSearchField;
        [SerializeField, HideInInspector] internal TextMeshProUGUI npcSearchGhost;
        [SerializeField, HideInInspector] internal GameObject npcListView;
        [SerializeField, HideInInspector] internal GameObject npcDetailView;
        [SerializeField, HideInInspector] internal TextMeshProUGUI npcListTitleText;
        [SerializeField, HideInInspector] internal GameObject npcListContent;
        [SerializeField, HideInInspector] internal TextMeshProUGUI npcDetailNameText;
        [SerializeField, HideInInspector] internal TextMeshProUGUI npcDetailStatsText;
        [SerializeField, HideInInspector] internal GameObject npcSellsContent;
        [SerializeField, HideInInspector] internal GameObject npcSpriteHost;
        [SerializeField, HideInInspector] internal RoSpriteRendererUI npcSpriteRenderer;

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
            if (npcListContent == null) return;
            ClearChildren(npcListContent.transform);
            npcRowEntries.Clear();

            if (data?.Items == null)
            {
                if (npcListTitleText != null) npcListTitleText.text = "NPCs";
                return;
            }

            if (npcListTitleText != null)
                npcListTitleText.text = $"NPCs ({data.Items.Count})";

            var ordered = data.Items.OrderBy(n => n.Map).ThenBy(n => n.Name).ToList();
            StartCoroutine(LoadListIncrementallyAsync(ordered, AddNpcListRow));
        }

        internal void AddNpcListRow(NpcEntry n)
        {
            var mapLabel = FormatMapLabel(n.Map, withBracket: false);
            var label = $"{n.Name}  <size=80%><color=#888888>({mapLabel})</color></size>";

            var row = CloneRow(rowTemplate, npcListContent.transform, 24);
            row.GetComponentInChildren<TextMeshProUGUI>(true).text = label;
            var captured = n;
            row.GetComponent<Button>().onClick.AddListener(() => ShowNpcDetail(captured));
            AttachRightClick(row, () => NetworkManager.Instance.SendMoveRequest(captured.Map, captured.X, captured.Y));
            npcRowEntries.Add((row, n, $"{n.Id} {n.Name} {n.Map} {mapLabel} {n.SpriteCode}"));
        }

        private void ShowNpcDetail(NpcEntry n)
        {
            npcListView.SetActive(false);
            npcDetailView.SetActive(true);
            currentNpcDetailId = n.Id;
            npcDetailNameText.text = FormatNameWithBracket(n.Name, n.Id.ToString());

            var sb = new StringBuilder();
            sb.AppendLine($"<b>Map:</b> {FormatMapLabel(n.Map)}");
            sb.AppendLine($"<b>Position:</b> ({n.X}, {n.Y})");
            sb.AppendLine($"<b>Type:</b> {(n.IsTrader ? "Trader" : "NPC")}");
            npcDetailStatsText.text = sb.ToString();

            ClearChildren(npcSellsContent.transform);
            if (n.SellsItems == null || n.SellsItems.Count == 0)
            {
                CreateInfoRow(npcSellsContent.transform, "(this NPC does not sell anything)");
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

            var row = CloneRow(iconRowTemplate, npcSellsContent.transform, 26, clickable: hasItem);
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
            if (npcSpriteHost == null) return;
            npcSpriteHost.SetActive(false);
            npcIdleFrameCount = 0;
            npcFrameTimer = 0f;

            if (string.IsNullOrEmpty(spriteCode)) return;
            EnsureNpcClassByCode();
            if (!npcClassByCode.TryGetValue(spriteCode, out var cls) || string.IsNullOrEmpty(cls.SpriteName))
                return;

            var path = NpcSpriteBasePath + cls.SpriteName;
            AddressableUtility.LoadRoSpriteData(npcSpriteHost, path, OnNpcSpriteLoaded);
        }

        private void OnNpcSpriteLoaded(RoSpriteData data)
        {
            if (data == null || npcSpriteRenderer == null) return;
            npcSpriteRenderer.SpriteData = data;
            npcSpriteRenderer.ActionId = 0;
            npcSpriteRenderer.Direction = Direction.South;
            npcSpriteRenderer.CurrentFrame = 0;

            var actionIdx = (int)Direction.South;
            if (actionIdx < data.Actions.Length)
                npcIdleFrameCount = data.Actions[actionIdx].Frames.Length;

            FitDetailSpriteToFrame(npcSpriteHost, npcSpriteRenderer, data, 0, Direction.SouthEast, NpcSpriteFitSize, NpcSpriteNaturalScale);

            npcSpriteHost.SetActive(true);
            npcSpriteRenderer.SetActive(true);
            npcSpriteRenderer.SetVerticesDirty();
            npcSpriteRenderer.SetMaterialDirty();
        }

        private void JumpToNpc(NpcEntry npc)
        {
            ShowTab(4);
            ShowNpcDetail(npc);
        }

        private void ReturnToNpcList()
        {
            if (npcListView != null) npcListView.SetActive(true);
            if (npcDetailView != null) npcDetailView.SetActive(false);
            if (npcSpriteHost != null) npcSpriteHost.SetActive(false);
            npcIdleFrameCount = 0;
        }

        internal static readonly PredicateRegistry<NpcEntry> NpcPredicates = BuildPredicateRegistry<NpcEntry>();

        private void FilterNpcs(string query) => ApplyFilter(npcRowEntries, query, (n, p) => NpcPredicates.TryMatch(n, p, out var r) && r);
    }
}
