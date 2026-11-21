using System;
using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI.Utility;
using Assets.Scripts.Utility;
using RebuildSharedData.ClientTypes;
using Assets.Scripts.UI.Hud;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.ClientDatabase
{
    public partial class ClientDatabaseWindow
    {
        private static readonly int s_secondaryTex = Shader.PropertyToID("_SecondaryTex");
        private readonly MinimapArt minimapArt = new();
        private Coroutine minimapLoad;
        private float minimapContentScale = 1f;

        private readonly List<ClientMapEntry> filteredMaps = new();
        private string currentMapDetailCode;
        private Material minimapMaterialInstance;
        private readonly Dictionary<string, List<string>> mapWarpLookup = new();
        private readonly Dictionary<string, List<PortalEntry>> mapPortalsLookup = new();
        private readonly Dictionary<string, List<MapMonsterSpawn>> mapMonstersLookup = new();
        private readonly Dictionary<string, int> portalConnectionIndex = new();
        private readonly List<GameObject> portalMarkers = new();
        private readonly List<GameObject> npcMarkers = new();
        private const string PortalMarkerPrefix = "PortalMarker_";
        private const string NpcMarkerPrefix = "NpcMarker_";

        [Serializable]
        private class PortalEntry
        {
            public string To;
            public int X;
            public int Y;
        }

        [Serializable]
        private class MapWarpEntry
        {
            public string Map;
            public List<string> ConnectedTo;
            public List<PortalEntry> Portals;
        }

        [Serializable]
        private class MapWarpFile
        {
            public List<MapWarpEntry> Items;
        }

        private struct MapMonsterSpawn
        {
            public int MonsterId;
            public string MonsterName;
            public int Count;
        }

        [SerializeField, HideInInspector] internal TextMeshProUGUI mapSearchGhost;
        [SerializeField, HideInInspector] internal Image mapDetailMinimap;
        //one unit per tile from the map's bottom left
        [SerializeField, HideInInspector] internal RectTransform mapDetailContent;
        [SerializeField, HideInInspector] internal GameObject portalMarkerTemplate;
        [SerializeField, HideInInspector] internal GameObject npcMarkerTemplate;

        private readonly HashSet<string> sortedMapMonsterKeys = new();

        internal static readonly PredicateRegistry<ClientMapEntry> MapPredicates = BuildPredicateRegistry<ClientMapEntry>();

        private void EnsureRuntimeMinimapMaterial()
        {
            if (mapDetailMinimap == null || minimapMaterialInstance != null) return;
            var source = mapDetailMinimap.material != null ? mapDetailMinimap.material : MinimapMaterial;
            if (source == null) return;

            minimapMaterialInstance = new Material(source) { name = "MonsterDb_Minimap" };
            mapDetailMinimap.material = minimapMaterialInstance;

            mapDetailMinimap.raycastTarget = true;
            AttachRightClick(mapDetailMinimap.gameObject, OnMinimapRightClick);
        }

        private void OnMinimapRightClick()
        {
            if (string.IsNullOrEmpty(currentMapDetailCode) || mapDetailMinimap.sprite == null) return;

            var cam = mapDetailMinimap.canvas != null ? mapDetailMinimap.canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(mapDetailContent, Input.mousePosition, cam, out var tile)) return;

            var size = mapDetailContent.sizeDelta;
            if (tile.x < 0 || tile.y < 0 || tile.x >= size.x || tile.y >= size.y) return;

            NetworkManager.Instance.SendMoveRequest(currentMapDetailCode, (int)tile.x, (int)tile.y);
        }

        private void PopulateMapList()
        {
            mapPage.Refresh();
        }

        private void BindMapListRow(DatabaseListRow row, ClientMapEntry map, int index)
        {
            row.SetLabel(FormatMapLabel(map.Code));
            row.SetIcon(null);
            row.SetActions(
                () => ShowMapDetail(map),
                () => NetworkManager.Instance.SendMoveRequest(map.Code));
        }

        private void ShowMapDetail(ClientMapEntry map)
        {
            mapPage.ShowDetail();
            currentMapDetailCode = map.Code;

            mapPage.DetailTitleText.text = FormatMapLabel(map.Code);

            PopulateMapConnections(map.Code);
            PopulateMapMonsters(map.Code);
            PopulateMapNpcs(map.Code);
            LoadMinimap(map.Code);
        }

        public void ReturnToMapList()
        {
            mapPage.ShowList();
        }

        private void LoadMinimap(string mapCode)
        {
            if (minimapLoad != null)
                StopCoroutine(minimapLoad);
            ClearMinimap();
            minimapLoad = StartCoroutine(LoadMinimapRoutine(mapCode));
        }

        private IEnumerator LoadMinimapRoutine(string mapCode)
        {
            yield return minimapArt.Load(mapCode);
            minimapLoad = null;
            if (!minimapArt.IsLoaded)
                yield break;

            minimapArt.ShowOn(mapDetailMinimap, minimapMaterialInstance);
            mapDetailMinimap.color = Color.white;
            FitMinimapContent(minimapArt.Content, minimapArt.TileWidth, minimapArt.TileHeight);
            DrawPortalMarkers(mapCode);
            DrawNpcMarkers(mapCode);
        }

        private void FitMinimapContent(RectInt drawn, int tileWidth, int tileHeight)
        {
            Canvas.ForceUpdateCanvases();
            var view = ((RectTransform)mapDetailContent.parent).rect.size;
            minimapContentScale = Mathf.Min(view.x / drawn.width, view.y / drawn.height);
            mapDetailContent.sizeDelta = new Vector2(tileWidth, tileHeight);
            mapDetailContent.localScale = new Vector3(minimapContentScale, minimapContentScale, 1f);
            mapDetailContent.anchoredPosition = -drawn.center * minimapContentScale;
        }

        private void ClearMinimap()
        {
            ReleaseMarkers(portalMarkers);
            ReleaseMarkers(npcMarkers);

            mapDetailMinimap.sprite = null;
            mapDetailMinimap.color = new Color(1, 1, 1, 0.08f);
            if (minimapMaterialInstance != null)
                minimapMaterialInstance.SetTexture(s_secondaryTex, null);
            minimapArt.Release();

            var view = ((RectTransform)mapDetailContent.parent).rect.size;
            minimapContentScale = 1f;
            mapDetailContent.sizeDelta = view;
            mapDetailContent.localScale = Vector3.one;
            mapDetailContent.anchoredPosition = -view / 2f;
            mapDetailMinimap.rectTransform.anchorMin = Vector2.zero;
            mapDetailMinimap.rectTransform.anchorMax = Vector2.one;
        }

        private void DrawPortalMarkers(string mapCode)
        {
            if (!mapPortalsLookup.TryGetValue(mapCode, out var portals) || portals == null)
                return;

            foreach (var p in portals)
            {
                if (portalConnectionIndex.TryGetValue(p.To, out var number))
                    portalMarkers.Add(PlaceMarker(portalMarkerTemplate, PortalMarkerPrefix, number, p.X, p.Y));
            }
        }

        private GameObject PlaceMarker(GameObject template, string prefix, int number, int tileX, int tileY)
        {
            var marker = GetPooledObject(template, mapDetailContent, () => Instantiate(template, mapDetailContent));
            marker.name = $"{prefix}{number}";
            var rt = (RectTransform)marker.transform;
            rt.anchoredPosition = new Vector2(tileX + 0.5f, tileY + 0.5f);
            rt.localScale = Vector3.one / minimapContentScale;
            var text = marker.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null)
                text.text = number.ToString();
            return marker;
        }

        private static void ReleaseMarkers(List<GameObject> markers)
        {
            foreach (var marker in markers)
                marker.SetActive(false);
            markers.Clear();
        }

        private void PopulateMapConnections(string mapCode)
        {
            ReleaseDetailRows(mapPage.PrimaryContent);
            portalConnectionIndex.Clear();
            if (!mapWarpLookup.TryGetValue(mapCode, out var connected) || connected.Count == 0)
            {
                CreateInfoRow(mapPage.PrimaryContent, "(no connections)");
                return;
            }

            for (var i = 0; i < connected.Count; i++)
            {
                var target = connected[i];
                var rowIndex = i + 1;
                portalConnectionIndex[target] = rowIndex;
                var hasMap = MapLookup.TryGetValue(target, out var targetMap);

                var row = GetDetailRow(rowTemplate, mapPage.PrimaryContent, 22, clickable: hasMap);
                row.GetComponentInChildren<TextMeshProUGUI>(true).text = $"<b>{rowIndex}.</b>  {FormatMapLabel(target)}";
                if (hasMap)
                {
                    var captured = targetMap;
                    row.GetComponent<Button>().onClick.AddListener(() => ShowMapDetail(captured));
                    AttachRightClick(row, () => NetworkManager.Instance.SendMoveRequest(captured.Code));
                }
            }
        }

        private void PopulateMapMonsters(string mapCode)
        {
            ReleaseDetailRows(mapPage.SecondaryContent);
            if (!mapMonstersLookup.TryGetValue(mapCode, out var spawns) || spawns.Count == 0)
            {
                CreateInfoRow(mapPage.SecondaryContent, "(no monsters)");
                return;
            }

            if (sortedMapMonsterKeys.Add(mapCode))
                spawns.Sort(static (a, b) => b.Count.CompareTo(a.Count));

            foreach (var s in spawns)
            {
                var hasMon = monsterLookup.TryGetValue(s.MonsterId, out var mon);
                var row = GetDetailRow(rowTemplate, mapPage.SecondaryContent, 22, clickable: hasMon);
                row.GetComponentInChildren<TextMeshProUGUI>(true).text = $"{s.MonsterName}  —  {s.Count}";
                if (hasMon)
                {
                    var captured = mon;
                    row.GetComponent<Button>().onClick.AddListener(() => JumpToMonster(captured));
                    AttachRightClick(row, () => NetworkManager.Instance.SendAdminSummonMonster(captured.Code, 1));
                }
            }
        }
        
        private void PopulateMapNpcs(string mapCode)
        {
            ReleaseDetailRows(mapPage.TertiaryContent);
            if (!mapNpcsLookup.TryGetValue(mapCode, out var npcs) || npcs.Count == 0)
            {
                CreateInfoRow(mapPage.TertiaryContent, "(no NPCs)");
                return;
            }

            for (var i = 0; i < npcs.Count; i++)
            {
                var npc = npcs[i];
                var markerIndex = i + 1;
                var row = GetDetailRow(rowTemplate, mapPage.TertiaryContent, 22);
                row.GetComponentInChildren<TextMeshProUGUI>(true).text =
                    $"<b>{markerIndex}.</b>  {npc.Name}{(npc.IsTrader ? "  <size=80%><color=#888888>[Trader]</color></size>" : "")}";
                var captured = npc;
                row.GetComponent<Button>().onClick.AddListener(() => JumpToNpc(captured));
                AttachRightClick(row, () => NetworkManager.Instance.SendMoveRequest(captured.Map, captured.X, captured.Y));
            }
        }

        private void DrawNpcMarkers(string mapCode)
        {
            if (!mapNpcsLookup.TryGetValue(mapCode, out var npcs) || npcs == null)
                return;

            for (var i = 0; i < npcs.Count; i++)
                npcMarkers.Add(PlaceMarker(npcMarkerTemplate, NpcMarkerPrefix, i + 1, npcs[i].X, npcs[i].Y));
        }

        private void FilterMaps(string query) =>
            ApplyDatabaseFilter<ClientMapEntry, DatabaseListRow>(
                MapLookup.Values,
                filteredMaps,
                query,
                mapPage.VirtualList,
                nameof(mapPage),
                "Maps",
                mapPage.TitleText,
                BindMapListRow,
                static map => $"{map.Code} {map.Name}",
                MapPredicates,
                sort: static (left, right) => string.Compare(left.Code, right.Code, StringComparison.Ordinal));
    }
}
