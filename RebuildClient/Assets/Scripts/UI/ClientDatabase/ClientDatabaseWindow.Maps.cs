using System;
using System.Collections.Generic;
using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI.Utility;
using Assets.Scripts.Utility;
using RebuildSharedData.ClientTypes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.ClientDatabase
{
    public partial class ClientDatabaseWindow
    {
        private static readonly int s_secondaryTex = Shader.PropertyToID("_SecondaryTex");
        private const float MinimapPxPerTile = 1f;

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
        [SerializeField, HideInInspector] internal GameObject portalMarkerTemplate;
        [SerializeField, HideInInspector] internal GameObject npcMarkerTemplate;

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
            if (string.IsNullOrEmpty(currentMapDetailCode)) return;
            if (mapDetailMinimap == null || mapDetailMinimap.sprite == null) return;

            var rt = mapDetailMinimap.rectTransform;
            var cam = mapDetailMinimap.canvas != null ? mapDetailMinimap.canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, Input.mousePosition, cam, out var local)) return;

            var rect = rt.rect;
            var pxFromLeft = local.x - rect.xMin;
            var pyFromTop = rect.yMax - local.y;

            var sprW = mapDetailMinimap.sprite.texture.width;
            var sprH = mapDetailMinimap.sprite.texture.height;
            var scale = Mathf.Min(rect.width / sprW, rect.height / sprH);
            var letterboxX = (rect.width - sprW * scale) * 0.5f;
            var letterboxY = (rect.height - sprH * scale) * 0.5f;

            var pixelX = (pxFromLeft - letterboxX) / scale;
            var pixelYFromTop = (pyFromTop - letterboxY) / scale;
            if (pixelX < 0 || pixelX >= sprW || pixelYFromTop < 0 || pixelYFromTop >= sprH) return;

            var tileX = (int)(pixelX / MinimapPxPerTile);
            var tileY = (int)((sprH - pixelYFromTop) / MinimapPxPerTile);

            NetworkManager.Instance.SendMoveRequest(currentMapDetailCode, tileX, tileY);
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
            mapDetailMinimap.sprite = null;
            mapDetailMinimap.color = new Color(1, 1, 1, 0.08f);

            if (minimapMaterialInstance != null)
                minimapMaterialInstance.SetTexture(s_secondaryTex, null);
            ClearPortalMarkers();
            ClearNpcMarkers();

            var path = $"Assets/Maps/minimap/{mapCode}.png";
            if (!ClientDataLoader.DoesAddressableExist<Sprite>(path)) return;

            var requested = mapCode;
            AddressableUtility.LoadSprite(gameObject, path, sprite =>
            {
                if (sprite == null || currentMapDetailCode != requested) return;
                mapDetailMinimap.sprite = sprite;
                mapDetailMinimap.color = Color.white;
                DrawPortalMarkers(requested, sprite);
                DrawNpcMarkers(requested, sprite);
            });

            var walkPath = $"Assets/Maps/minimap/{mapCode}_walkmask.png";
            if (ClientDataLoader.DoesAddressableExist<Sprite>(walkPath))
            {
                AddressableUtility.LoadSprite(gameObject, walkPath, walk =>
                {
                    if (walk == null || currentMapDetailCode != requested || minimapMaterialInstance == null) return;
                    minimapMaterialInstance.SetTexture(s_secondaryTex, walk.texture);
                });
            }
        }

        private void ClearPortalMarkers()
        {
            for (var i = portalMarkers.Count - 1; i >= 0; i--)
            {
                if (portalMarkers[i] != null)
                    portalMarkers[i].SetActive(false);
            }
            portalMarkers.Clear();
        }

        private void DrawPortalMarkers(string mapCode, Sprite minimapSprite)
        {
            ClearPortalMarkers();
            if (minimapSprite == null) return;
            if (!mapPortalsLookup.TryGetValue(mapCode, out var portals)) return;
            if (portals == null || portals.Count == 0) return;

            var imageRT = mapDetailMinimap.rectTransform;
            LayoutRebuilder.ForceRebuildLayoutImmediate(imageRT);

            var imgW = imageRT.rect.width;
            var imgH = imageRT.rect.height;
            var sprW = minimapSprite.texture.width;
            var sprH = minimapSprite.texture.height;
            if (imgW <= 0 || imgH <= 0 || sprW <= 0 || sprH <= 0) return;

            var scale = Mathf.Min(imgW / sprW, imgH / sprH);
            var letterboxX = (imgW - sprW * scale) * 0.5f;
            var letterboxY = (imgH - sprH * scale) * 0.5f;

            foreach (var p in portals)
            {
                if (!portalConnectionIndex.TryGetValue(p.To, out var idx)) continue;

                var pixelYFromTop = sprH - p.Y * MinimapPxPerTile;
                var posX = letterboxX + p.X * MinimapPxPerTile * scale;
                var posY = letterboxY + pixelYFromTop * scale;

                CreatePortalMarker(idx, posX, posY);
            }
        }

        private void CreatePortalMarker(int number, float xFromLeft, float yFromTop)
        {
            if (portalMarkerTemplate == null)
            {
                Debug.LogWarning("ClientDatabaseWindow: portalMarkerTemplate is unset on the Database prefab.");
                return;
            }

            var marker = GetPooledObject(
                portalMarkerTemplate,
                mapDetailMinimap.transform,
                () => Instantiate(portalMarkerTemplate, mapDetailMinimap.transform));
            marker.name = $"{PortalMarkerPrefix}{number}";
            var mrt = marker.GetComponent<RectTransform>();
            mrt.anchoredPosition = new Vector2(xFromLeft, -yFromTop);
            var tmp = marker.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null) tmp.text = number.ToString();

            portalMarkers.Add(marker);
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

        private readonly HashSet<string> sortedMapMonsterKeys = new();

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

        private void ClearNpcMarkers()
        {
            for (var i = npcMarkers.Count - 1; i >= 0; i--)
            {
                if (npcMarkers[i] != null)
                    npcMarkers[i].SetActive(false);
            }
            npcMarkers.Clear();
        }

        private void DrawNpcMarkers(string mapCode, Sprite minimapSprite)
        {
            ClearNpcMarkers();
            if (minimapSprite == null || npcMarkerTemplate == null) return;
            if (!mapNpcsLookup.TryGetValue(mapCode, out var npcs)) return;
            if (npcs == null || npcs.Count == 0) return;

            var imageRT = mapDetailMinimap.rectTransform;
            LayoutRebuilder.ForceRebuildLayoutImmediate(imageRT);

            var imgW = imageRT.rect.width;
            var imgH = imageRT.rect.height;
            var sprW = minimapSprite.texture.width;
            var sprH = minimapSprite.texture.height;
            if (imgW <= 0 || imgH <= 0 || sprW <= 0 || sprH <= 0) return;

            var scale = Mathf.Min(imgW / sprW, imgH / sprH);
            var letterboxX = (imgW - sprW * scale) * 0.5f;
            var letterboxY = (imgH - sprH * scale) * 0.5f;

            for (var i = 0; i < npcs.Count; i++)
            {
                var npc = npcs[i];
                var pixelYFromTop = sprH - npc.Y * MinimapPxPerTile;
                var posX = letterboxX + npc.X * MinimapPxPerTile * scale;
                var posY = letterboxY + pixelYFromTop * scale;

                CreateNpcMarker(i + 1, posX, posY);
            }
        }

        private void CreateNpcMarker(int number, float xFromLeft, float yFromTop)
        {
            if (npcMarkerTemplate == null) return;

            var marker = GetPooledObject(
                npcMarkerTemplate,
                mapDetailMinimap.transform,
                () => Instantiate(npcMarkerTemplate, mapDetailMinimap.transform));
            marker.name = $"{NpcMarkerPrefix}{number}";
            var mrt = marker.GetComponent<RectTransform>();
            mrt.anchoredPosition = new Vector2(xFromLeft, -yFromTop);
            var tmp = marker.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmp != null) tmp.text = number.ToString();

            npcMarkers.Add(marker);
        }

        internal static readonly PredicateRegistry<ClientMapEntry> MapPredicates = BuildPredicateRegistry<ClientMapEntry>();

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
