using System;
using System.Collections.Generic;
using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI.Utility;
using RebuildSharedData.ClientTypes;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Assets.Scripts.UI.ClientDatabase
{
    public partial class ClientDatabaseWindow : WindowBase
    {
        public TMP_FontAsset TmpFont;
        public Material MinimapMaterial;

        [SerializeField, HideInInspector] internal RectTransform panelRT;
        [SerializeField, HideInInspector] internal GameObject rowTemplate;
        [SerializeField, HideInInspector] internal Material flatTmpMaterial;
        [SerializeField, HideInInspector] internal GameObject helpContainer;
        [SerializeField] private TabGroupVisual tabGroup;
        
        private readonly Dictionary<int, MonsterEntry> monsterLookup = new();
        private readonly Dictionary<int, List<DroppedBy>> dropMap = new();
        private bool tabIconsInitialized;

        [SerializeField] private ClientDatabasePage monsterPage;
        [SerializeField] private ClientDatabasePage itemPage;
        [SerializeField] private ClientDatabasePage npcPage;
        [SerializeField] private ClientDatabasePage mapPage;
        
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
            InitializePages();
            WireAutocompleteGhosts();
            WireDetailPortraitClicks();
            EnsureRuntimeMinimapMaterial();
            if (portalMarkerTemplate != null) portalMarkerTemplate.SetActive(false);
            if (npcMarkerTemplate != null) npcMarkerTemplate.SetActive(false);
            if (rowTemplate != null) rowTemplate.SetActive(false);
            LoadDatabaseData();
            InitializeTabIcons();
            if (helpContentText != null) helpContentText.text = BuildHelpText();
            ShowSelectedTab();
        }
        
        public override void OnDestroy()
        {
            ClientDataLoader.InitializationCompleted -= RefreshLookupPages;
            if (minimapMaterialInstance)
                Destroy(minimapMaterialInstance);
            base.OnDestroy();
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            TryHandleItemDetailLinkClick(eventData);
        }

        private void OnEnable()
        {
            InitializePages();
            ClientDataLoader.InitializationCompleted += RefreshLookupPages;
            if (ClientDataLoader.Instance?.IsInitialized == true)
                RefreshLookupPages();
        }

        private void OnDisable()
        {
            ClientDataLoader.InitializationCompleted -= RefreshLookupPages;
        }

        private void Update()
        {
            var monFrames = monsterSprite.FrameCount;
            if (monFrames > 1 && monsterSprite.gameObject.activeInHierarchy)
            {
                frameTimer += Time.deltaTime;
                if (frameTimer > 0.18f)
                {
                    frameTimer = 0f;
                    monsterFrame = (monsterFrame + 1) % monFrames;
                    monsterSprite.SetFrame(monsterFrame);
                }
            }

            var npcFrames = npcSprite.FrameCount;
            if (npcFrames > 1 && npcSprite.gameObject.activeInHierarchy)
            {
                npcFrameTimer += Time.deltaTime;
                if (npcFrameTimer > 0.18f)
                {
                    npcFrameTimer = 0f;
                    npcFrame = (npcFrame + 1) % npcFrames;
                    npcSprite.SetFrame(npcFrame);
                }
            }

            HandleAutocompleteInput();
        }

        private void RefreshLookupPages()
        {
            LoadDatabaseData();
            InitializeTabIcons();
            PopulateItemList();
            PopulateMapList();
        }

        private void InitializeTabIcons()
        {
            if (tabIconsInitialized || ClientDataLoader.Instance?.IsInitialized != true)
                return;

            tabIconsInitialized = true;

            tabGroup.SetTabIconFromRoSprite(0, "Assets/Sprites/Npcs/4_orcwarrior2.spr");
            tabGroup.SetTabIconFromItemAtlas(1, "Angel's_Cardigan");
            tabGroup.SetTabIconFromRoSprite(2, "Assets/Sprites/Npcs/4_f_kafra1.spr");
            tabGroup.SetTabIconFromItemAtlas(3, "Torn_Scroll");
            tabGroup.SetTabIconFromRoSprite(4, "Assets/Sprites/Misc/emotion.spr", "sprite_emotion_0018");
        }
        
        private void WireDetailPortraitClicks()
        {
            if (monsterSprite != null && monsterSprite.transform.parent != null)
            {
                AttachRightClick(monsterSprite.transform.parent.gameObject, () =>
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

            if (npcSprite != null && npcSprite.transform.parent != null)
            {
                AttachRightClick(npcSprite.transform.parent.gameObject, () =>
                {
                    if (npcLookup.TryGetValue(currentNpcDetailId, out var n))
                        NetworkManager.Instance.SendMoveRequest(n.Map, n.X, n.Y);
                });
            }
        }

        private void InitializePages()
        {
            monsterPage.Initialize("Monsters", rowTemplate, FilterMonsters, null, "Drops", "Spawns");
            itemPage.Initialize("Items", rowTemplate, FilterItems, null, "Dropped By", "Sold By");
            npcPage.Initialize("NPCs", rowTemplate, FilterNpcs, null, "Sells", null);
            mapPage.Initialize("Maps", rowTemplate, FilterMaps, "Connections", "Monsters", "NPCs");
        }

        private void ShowSelectedTab()
        {
            if (tabGroup.SelectedTabIndex < 0)
                tabGroup.SelectTab(0);
            else
                OnTabSelected(tabGroup.SelectedTabIndex);
        }

        public void OnTabSelected(int selectedTabIndex)
        {
            InitializePages();
            monsterPage.SetActive(selectedTabIndex == 0);
            itemPage.SetActive(selectedTabIndex == 1);
            npcPage.SetActive(selectedTabIndex == 2);
            mapPage.SetActive(selectedTabIndex == 3);
            helpContainer.SetActive(selectedTabIndex == 4);

            switch (selectedTabIndex)
            {
                case 0:
                    ReturnToMonsterList();
                    break;
                case 1:
                    ReturnToItemList();
                    break;
                case 2:
                    ReturnToNpcList();
                    break;
                case 3:
                    ReturnToMapList();
                    break;
            }
        }

        private void JumpToItem(ItemData item)
        {
            tabGroup.SelectTab(1);
            ShowItemDetail(item);
        }

        private void JumpToMonster(MonsterEntry mon)
        {
            tabGroup.SelectTab(0);
            ShowMonsterDetail(mon);
        }

        private void JumpToMap(ClientMapEntry map)
        {
            tabGroup.SelectTab(3);
            ShowMapDetail(map);
        }
        
        private bool dbDataLoaded;
        
        private void LoadDatabaseData()
        {
            if (dbDataLoaded || ClientDataLoader.Instance == null || !ClientDataLoader.Instance.IsInitialized)
                return;

            var monsterDb = LoadMonsterDatabase();
            var npcDb = LoadNpcDatabase();
            if (monsterDb?.Items == null || npcDb?.Items == null)
                return;

            LoadMapWarps();
            BuildMonsterReverseLookups(monsterDb);
            BuildMonsterValueIndex(monsterDb);
            BuildNpcReverseLookups(npcDb);

            PopulateMonsterList(monsterDb);
            PopulateNpcList(npcDb);
            dbDataLoaded = true;
        }
        
        private MonsterDbFile LoadMonsterDatabase() => LoadStreamingJson<MonsterDbFile>("ClientConfigGenerated/monsterdatabase.json");

        private void LoadMapWarps()
        {
            var warpData = LoadStreamingJson<MapWarpFile>("ClientConfigGenerated/mapwarps.json");
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
