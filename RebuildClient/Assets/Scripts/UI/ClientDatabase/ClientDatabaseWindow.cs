using System;
using System.Collections.Generic;
using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using RebuildSharedData.ClientTypes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.ClientDatabase
{
    public partial class ClientDatabaseWindow : WindowBase
    {
        public TMP_FontAsset TmpFont;
        public Material MinimapMaterial;

        [SerializeField, HideInInspector] internal RectTransform panelRT;
        [SerializeField, HideInInspector] internal Button closeButton;
        [SerializeField, HideInInspector] internal GameObject rowTemplate;
        [SerializeField, HideInInspector] internal GameObject iconRowTemplate;
        [SerializeField, HideInInspector] internal Material flatTmpMaterial;
        [SerializeField, HideInInspector] internal GameObject helpContainer;
        [SerializeField, HideInInspector] internal Image helpTabImage;
        
        private readonly Dictionary<int, MonsterEntry> monsterLookup = new();
        private readonly Dictionary<int, List<DroppedBy>> dropMap = new();
        
        private static Dictionary<int, ItemData> ItemLookup => ClientDataLoader.Instance?.ItemIdLookup ?? EmptyItemLookup;
        private static Dictionary<int, MonsterClassData> ClassLookup => ClientDataLoader.Instance?.MonsterClassLookup ?? EmptyClassLookup;
        private static Dictionary<string, ClientMapEntry> MapLookup => ClientDataLoader.Instance?.MapDataLookup ?? EmptyMapLookup;
        private static Dictionary<string, string> DescLookup => ClientDataLoader.Instance?.ItemDescriptionTable ?? EmptyDescLookup;

        private static readonly Dictionary<int, ItemData> EmptyItemLookup = new();
        private static readonly Dictionary<int, MonsterClassData> EmptyClassLookup = new();
        private static readonly Dictionary<string, ClientMapEntry> EmptyMapLookup = new();
        private static readonly Dictionary<string, string> EmptyDescLookup = new();

        private void Start()
        {
            RewireStructuralButtons();
            WireAutocompleteGhosts();
            WireDetailPortraitClicks();
            EnsureRuntimeMinimapMaterial();
            if (portalMarkerTemplate != null) portalMarkerTemplate.SetActive(false);
            if (rowTemplate != null) rowTemplate.SetActive(false);
            if (iconRowTemplate != null) iconRowTemplate.SetActive(false);
            LoadDataAndPopulate();
            if (helpContentText != null) helpContentText.text = BuildHelpText();
        }
        
        public new void OnDestroy()
        {
            if (UiManager.Instance)
                UiManager.Instance.WindowStack.Remove(this);
            if (minimapMaterialInstance)
                Destroy(minimapMaterialInstance);
        }

        private void OnEnable()
        {
            if (!itemsLoaded) PopulateItemList();
            if (!mapsLoaded) PopulateMapList();
        }

        private void Update()
        {
            var frameCount = idleFrameCount;
            if (monsterSpriteRenderer && frameCount > 1 && monsterSpriteRenderer.gameObject.activeInHierarchy)
            {
                frameTimer += Time.deltaTime;
                if (frameTimer > 0.18f)
                {
                    frameTimer = 0f;
                    monsterSpriteRenderer.CurrentFrame = (monsterSpriteRenderer.CurrentFrame + 1) % frameCount;
                    monsterSpriteRenderer.SetVerticesDirty();
                }
            }

            HandleAutocompleteInput();
        }
        
        private void WireDetailPortraitClicks()
        {
            if (monsterSpriteHost != null && monsterSpriteHost.transform.parent != null)
            {
                AttachRightClick(monsterSpriteHost.transform.parent.gameObject, () =>
                {
                    if (monsterLookup.TryGetValue(currentMonsterDetailId, out var m))
                        NetworkManager.Instance.SendAdminSummonMonster(m.Code, 1);
                });
            }

            if (itemDetailIcon != null && itemDetailIcon.transform.parent != null)
            {
                AttachRightClick(itemDetailIcon.transform.parent.gameObject, () =>
                {
                    if (currentItemDetailId >= 0)
                        NetworkManager.Instance.SendAdminCreateItem(currentItemDetailId, 1);
                });
            }
        }

        private void RewireStructuralButtons()
        {
            WireButton(monstersTabImage, () => ShowTab(0));
            WireButton(itemsTabImage, () => ShowTab(1));
            WireButton(mapsTabImage, () => ShowTab(2));
            WireButton(helpTabImage, () => ShowTab(3));

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(HideWindow);
            }

            if (monsterBackButton != null)
            {
                monsterBackButton.onClick.RemoveAllListeners();
                monsterBackButton.onClick.AddListener(ReturnToMonsterList);
            }
            if (itemBackButton != null)
            {
                itemBackButton.onClick.RemoveAllListeners();
                itemBackButton.onClick.AddListener(ReturnToItemList);
            }
            if (mapBackButton != null)
            {
                mapBackButton.onClick.RemoveAllListeners();
                mapBackButton.onClick.AddListener(ReturnToMapList);
            }

            WireSearchField(monsterSearchField, FilterMonsters);
            WireSearchField(itemSearchField, FilterItems);
            WireSearchField(mapSearchField, FilterMaps);
        }
        
        private const float FilterDebounceSeconds = 0.1f;
        private UnityEngine.Events.UnityAction<string> pendingFilterHandler;
        private string pendingFilterQuery;

        private void WireSearchField(TMP_InputField field, UnityEngine.Events.UnityAction<string> handler)
        {
            if (field == null) return;
            field.onValueChanged.RemoveAllListeners();
            field.onValueChanged.AddListener(q => ScheduleFilter(handler, q));
        }

        private void ScheduleFilter(UnityEngine.Events.UnityAction<string> handler, string query)
        {
            pendingFilterHandler = handler;
            pendingFilterQuery = query;
            CancelInvoke(nameof(RunPendingFilter));
            Invoke(nameof(RunPendingFilter), FilterDebounceSeconds);
        }

        private void RunPendingFilter() => pendingFilterHandler?.Invoke(pendingFilterQuery);

        private static void WireButton(Image tabImage, UnityEngine.Events.UnityAction action)
        {
            if (tabImage == null) return;
            var btn = tabImage.GetComponent<Button>();
            if (btn == null) return;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(action);
        }

        private void ShowTab(int idx)
        {
            monstersContainer.SetActive(idx == 0);
            itemsContainer.SetActive(idx == 1);
            if (mapsContainer != null) mapsContainer.SetActive(idx == 2);
            if (helpContainer != null) helpContainer.SetActive(idx == 3);
            monstersTabImage.color = idx == 0 ? s_activeTabColor : s_inactiveTabColor;
            itemsTabImage.color = idx == 1 ? s_activeTabColor : s_inactiveTabColor;
            if (mapsTabImage != null) mapsTabImage.color = idx == 2 ? s_activeTabColor : s_inactiveTabColor;
            if (helpTabImage != null) helpTabImage.color = idx == 3 ? s_activeTabColor : s_inactiveTabColor;
        }

        private void JumpToItem(ItemData item)
        {
            ShowTab(1);
            ShowItemDetail(item);
        }

        private void JumpToMonster(MonsterEntry mon)
        {
            ShowTab(0);
            ShowMonsterDetail(mon);
        }

        private void JumpToMap(ClientMapEntry map)
        {
            ShowTab(2);
            ShowMapDetail(map);
        }
        
        private void LoadDataAndPopulate()
        {
            var monsterDb = LoadMonsterDatabase();
            LoadMapWarps();
            BuildMonsterReverseLookups(monsterDb);
            BuildMonsterValueIndex(monsterDb);

            PopulateMonsterList(monsterDb);
            PopulateItemList();
            PopulateMapList();
            ShowTab(0);
        }
        
        private MonsterDbFile LoadMonsterDatabase()
        {
            var dbJson = LoadStreamingFile("ClientConfigGenerated/monsterdatabase.json");
            if (string.IsNullOrEmpty(dbJson)) return null;
            try
            {
                return JsonUtility.FromJson<MonsterDbFile>(dbJson);
            }
            catch (Exception e)
            {
                Debug.LogError($"Bad monsterdatabase.json: {e.Message}");
                return null;
            }
        }

        private void LoadMapWarps()
        {
            var warpJson = LoadStreamingFile("ClientConfigGenerated/mapwarps.json");
            if (string.IsNullOrEmpty(warpJson)) return;
            var warpData = JsonUtility.FromJson<MapWarpFile>(warpJson);
            if (warpData?.Items == null) return;
            foreach (var wp in warpData.Items)
            {
                mapWarpLookup[wp.Map] = wp.ConnectedTo ?? new List<string>();
                mapPortalsLookup[wp.Map] = wp.Portals ?? new List<PortalEntry>();
            }
        }
        
        private void BuildMonsterReverseLookups(MonsterDbFile monsterDb)
        {
            if (monsterDb?.Items == null) return;

            foreach (var mon in monsterDb.Items)
            {
                monsterLookup[mon.Id] = mon;
                if (mon.Drops != null)
                {
                    foreach (var drop in mon.Drops)
                    {
                        if (!dropMap.TryGetValue(drop.ItemId, out var list))
                            dropMap[drop.ItemId] = list = new List<DroppedBy>();
                        list.Add(new DroppedBy
                        {
                            MonsterId = mon.Id,
                            MonsterName = mon.Name,
                            Chance = drop.Chance,
                        });
                    }
                }
                if (mon.Spawns != null)
                {
                    foreach (var spawn in mon.Spawns)
                    {
                        if (!mapMonstersLookup.TryGetValue(spawn.Map, out var list))
                            mapMonstersLookup[spawn.Map] = list = new List<MapMonsterSpawn>();
                        list.Add(new MapMonsterSpawn
                        {
                            MonsterId = mon.Id,
                            MonsterName = mon.Name,
                            Count = spawn.Count,
                        });
                    }
                }
            }
        }
    }
}
