using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using Assets.Scripts.Utility;
using RebuildSharedData.Enum;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.ClientDatabase
{
    public partial class ClientDatabaseWindow
    {
        private float frameTimer;
        private int idleFrameCount;
        private int currentMonsterDetailId = -1;

        private readonly List<(GameObject go, MonsterEntry entry, string searchText)> monsterRowEntries = new();

        private const string MonsterSpriteBasePath = "Assets/Sprites/Monsters/";

        private const float MonsterSpriteFitSize = 160f;
        private const float MonsterSpriteNaturalScale = 100f;

        [Serializable]
        internal class DropEntry
        {
            public int ItemId;
            public int Chance;
            public int CountMin;
            public int CountMax;
        }

        [Serializable]
        internal class SpawnEntry
        {
            public string Map;
            public int Count;
        }

        [Serializable] internal class MonsterEntry
        {
            public int Id;
            public string Code;
            public string Name;
            public int Level;
            public int HP;
            public int Exp;
            public int JExp;
            public int AtkMin;
            public int AtkMax;
            public int Def;
            public int MDef;
            public int Str;
            public int Agi;
            public int Vit;
            public int Int;
            public int Dex;
            public int Luk;
            public int Range;
            public int ScanDist;
            public float MoveSpeed;
            public string Size;
            public string Element;
            public string Race;
            public string Ai;
            public string Special;
            public List<string> Tags;
            public List<DropEntry> Drops;
            public List<SpawnEntry> Spawns;
        }

        [Serializable] private class MonsterDbFile { public List<MonsterEntry> Items; }

        private struct DroppedBy
        {
            public int MonsterId;
            public string MonsterName;
            public int Chance;
        }

        [SerializeField, HideInInspector] internal GameObject monstersContainer;
        [SerializeField, HideInInspector] internal Image monstersTabImage;
        [SerializeField, HideInInspector] internal Button monsterBackButton;
        [SerializeField, HideInInspector] internal TMP_InputField monsterSearchField;
        [SerializeField, HideInInspector] internal TextMeshProUGUI monsterSearchGhost;
        [SerializeField, HideInInspector] internal GameObject monsterListView;
        [SerializeField, HideInInspector] internal GameObject monsterDetailView;
        [SerializeField, HideInInspector] internal TextMeshProUGUI monsterListTitleText;
        [SerializeField, HideInInspector] internal GameObject monsterListContent;
        [SerializeField, HideInInspector] internal TextMeshProUGUI monsterDetailNameText;
        [SerializeField, HideInInspector] internal TextMeshProUGUI monsterDetailStatsText;
        [SerializeField, HideInInspector] internal GameObject dropsContent;
        [SerializeField, HideInInspector] internal GameObject spawnsContent;
        [SerializeField, HideInInspector] internal GameObject monsterSpriteHost;
        [SerializeField, HideInInspector] internal RoSpriteRendererUI monsterSpriteRenderer;

        private void PopulateMonsterList(MonsterDbFile data)
        {
            if (monsterListContent == null) return;
            ClearChildren(monsterListContent.transform);
            monsterRowEntries.Clear();

            if (data?.Items == null)
            {
                if (monsterListTitleText != null) monsterListTitleText.text = "Monsters";
                return;
            }

            if (monsterListTitleText != null)
                monsterListTitleText.text = $"Monsters ({data.Items.Count})";

            foreach (var m in data.Items)
            {
                AddMonsterListRow(m);
            }
        }

        internal void AddMonsterListRow(MonsterEntry m)
        {
            var row = CloneRow(rowTemplate, monsterListContent.transform, 24);
            row.GetComponentInChildren<TextMeshProUGUI>(true).text = $"{m.Name}  (Lv {m.Level})";
            var captured = m;
            row.GetComponent<Button>().onClick.AddListener(() => ShowMonsterDetail(captured));
            AttachRightClick(row, () => NetworkManager.Instance.SendAdminSummonMonster(captured.Code, 1));
            monsterRowEntries.Add((row, m, $"{m.Id} {m.Name} {m.Code}"));
        }

        private void ShowMonsterDetail(MonsterEntry m)
        {
            monsterListView.SetActive(false);
            monsterDetailView.SetActive(true);
            currentMonsterDetailId = m.Id;
            monsterDetailNameText.text = FormatNameWithBracket(m.Name, m.Id.ToString());
            
            var sb = new StringBuilder();
            sb.AppendLine($"<b>Level:</b> {m.Level}<pos=30%><b>Exp:</b> {m.Exp}<pos=80%><b>STR:</b> {m.Str}");
            sb.AppendLine($"<b>HP:</b> {m.HP}<pos=30%><b>JExp:</b> {m.JExp}<pos=80%><b>AGI:</b> {m.Agi}");
            sb.AppendLine($"<b>Atk:</b> {m.AtkMin}-{m.AtkMax}<pos=30%><b>Size:</b> {m.Size}<pos=80%><b>VIT:</b> {m.Vit}");
            sb.AppendLine($"<b>Def:</b> {m.Def}<pos=30%><b>Element:</b> {m.Element}<pos=80%><b>INT:</b> {m.Int}");
            sb.AppendLine($"<b>MDef:</b> {m.MDef}<pos=30%><b>Race:</b> {m.Race}<pos=80%><b>DEX:</b> {m.Dex}");
            sb.AppendLine($"<b>Range:</b> {m.Range}<pos=30%><b>AI:</b> {m.Ai}<pos=80%><b>LUK:</b> {m.Luk}");
            sb.AppendLine();
            var hitFor100 = m.Level + m.Agi + 25;
            var fleeFor95 = m.Level + m.Dex + 70;
            sb.AppendLine($"<b>Hit(100%):</b> {hitFor100}<pos=50%><b>Flee(95%):</b> {fleeFor95}");
            var expPerHp = m.HP > 0 ? m.Exp / (float)m.HP : 0f;
            var jExpPerHp = m.HP > 0 ? m.JExp / (float)m.HP : 0f;
            sb.AppendLine($"<b>Base Exp/HP:</b> {expPerHp:0.00}<pos=50%><b>Job Exp/HP:</b> {jExpPerHp:0.00}");
            if (m.Tags != null && m.Tags.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"<b>Tags:</b> {string.Join(", ", m.Tags)}");
            }
            monsterDetailStatsText.text = sb.ToString();

            ClearChildren(dropsContent.transform);
            if (m.Drops == null || m.Drops.Count == 0)
            {
                CreateInfoRow(dropsContent.transform, "(no drops)");
            }
            else
            {
                foreach (var d in m.Drops.OrderByDescending(d => d.Chance))
                    CreateDropRow(d);
            }

            ClearChildren(spawnsContent.transform);
            if (m.Spawns == null || m.Spawns.Count == 0)
            {
                CreateInfoRow(spawnsContent.transform, "(no live spawns)");
            }
            else
            {
                foreach (var s in m.Spawns.OrderByDescending(s => s.Count))
                {
                    CreateSpawnRow(s);
                }
            }

            LoadMonsterSprite(m.Id);
        }

        private void CreateDropRow(DropEntry d)
        {
            var hasItem = ItemLookup.TryGetValue(d.ItemId, out var item);
            var name = hasItem ? FormatItemName(item) : $"Item#{d.ItemId}";
            var pct = d.Chance / 100f;

            var row = CloneRow(iconRowTemplate, dropsContent.transform, 26, clickable: hasItem);
            row.GetComponentInChildren<TextMeshProUGUI>(true).text = $"{name}  —  {pct:0.##}%";

            var iconImage = row.transform.Find("Icon").GetComponent<Image>();
            var sprite = hasItem ? GetItemIcon(item.Sprite) : null;
            iconImage.sprite = sprite;
            iconImage.color = sprite != null ? Color.white : new Color(1, 1, 1, 0.08f);

            if (hasItem)
            {
                var captured = item;
                row.GetComponent<Button>().onClick.AddListener(() => JumpToItem(captured));
                AttachRightClick(row, () => NetworkManager.Instance.SendAdminCreateItem(captured.Id, 1));
            }
        }

        private void CreateSpawnRow(SpawnEntry s)
        {
            var hasMap = MapLookup.TryGetValue(s.Map, out var map);
            var displayName = hasMap && !string.IsNullOrEmpty(map.Name)
                ? FormatNameWithBracket(map.Name, s.Map)
                : s.Map;

            var row = CloneRow(rowTemplate, spawnsContent.transform, 22, clickable: hasMap);
            row.GetComponentInChildren<TextMeshProUGUI>(true).text = $"{displayName}  —  {s.Count}";
            if (hasMap)
            {
                var captured = map;
                row.GetComponent<Button>().onClick.AddListener(() => JumpToMap(captured));
                AttachRightClick(row, () => NetworkManager.Instance.SendMoveRequest(captured.Code));
            }
        }

        private void LoadMonsterSprite(int monsterId)
        {
            monsterSpriteHost.SetActive(false);
            idleFrameCount = 0;
            frameTimer = 0f;

            if (!ClassLookup.TryGetValue(monsterId, out var cls) || string.IsNullOrEmpty(cls.SpriteName))
            {
                Debug.LogWarning($"No sprite name available for monster {monsterId}");
                return;
            }

            var path = MonsterSpriteBasePath + cls.SpriteName;
            AddressableUtility.LoadRoSpriteData(monsterSpriteHost, path, OnMonsterSpriteLoaded);
        }

        private void OnMonsterSpriteLoaded(RoSpriteData data)
        {
            if (data == null || monsterSpriteRenderer == null) return;
            monsterSpriteRenderer.SpriteData = data;
            monsterSpriteRenderer.ActionId = 0;
            monsterSpriteRenderer.Direction = Direction.South;
            monsterSpriteRenderer.CurrentFrame = 0;

            var actionIdx = 0 + (int)Direction.South;
            if (actionIdx < data.Actions.Length)
                idleFrameCount = data.Actions[actionIdx].Frames.Length;

            FitMonsterSpriteToFrame(data, 0, Direction.SouthEast);

            monsterSpriteHost.SetActive(true);
            monsterSpriteRenderer.SetActive(true);
            monsterSpriteRenderer.SetVerticesDirty();
            monsterSpriteRenderer.SetMaterialDirty();
        }

        private void FitMonsterSpriteToFrame(
            RoSpriteData data, int action, Direction direction)
        {
            var dirIdx = (int)direction;
            var actionIdx = action + dirIdx;
            var hostRT = (RectTransform)monsterSpriteHost.transform;

            if (actionIdx >= data.Actions.Length || data.Actions[actionIdx].Frames.Length == 0)
            {
                hostRT.sizeDelta = new Vector2(MonsterSpriteFitSize, MonsterSpriteFitSize);
                monsterSpriteRenderer.OffsetPosition = Vector2.zero;
                return;
            }

            var frameCount = data.Actions[actionIdx].Frames.Length;
            var meshCache = SpriteMeshCache.GetMeshCacheForSprite(data.Name);

            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

            for (var f = 0; f < frameCount; f++)
            {
                var id = ((action + dirIdx) << 16) + f;
                if (!meshCache.TryGetValue(id, out var mesh))
                {
                    mesh = SpriteMeshBuilder.BuildSpriteMesh(data, action, dirIdx, f);
                    if (mesh != null) meshCache.Add(id, mesh);
                }
                if (mesh == null) continue;

                foreach (var v3 in mesh.vertices)
                {
                    var v = (Vector2)v3;
                    if (v.x < min.x) min.x = v.x;
                    if (v.y < min.y) min.y = v.y;
                    if (v.x > max.x) max.x = v.x;
                    if (v.y > max.y) max.y = v.y;
                }
            }

            var size = max - min;
            if (size.x <= 0f || size.y <= 0f || float.IsInfinity(min.x) || float.IsInfinity(max.x))
            {
                hostRT.sizeDelta = new Vector2(MonsterSpriteFitSize, MonsterSpriteFitSize);
                monsterSpriteRenderer.OffsetPosition = Vector2.zero;
                return;
            }

            var center = (min + max) * 0.5f;
            var maxDim = Mathf.Max(size.x, size.y);
            var scale = Mathf.Min(MonsterSpriteNaturalScale, MonsterSpriteFitSize / maxDim);

            hostRT.sizeDelta = new Vector2(scale, scale);
            monsterSpriteRenderer.OffsetPosition = -center * 50f;
        }

        private void ReturnToMonsterList()
        {
            if (monsterListView != null) monsterListView.SetActive(true);
            if (monsterDetailView != null) monsterDetailView.SetActive(false);
            if (monsterSpriteHost != null) monsterSpriteHost.SetActive(false);
            idleFrameCount = 0;
        }
        
        internal static readonly PredicateRegistry<MonsterEntry> MonsterPredicates = BuildPredicateRegistry<MonsterEntry>();

        private void FilterMonsters(string query) => ApplyFilter(monsterRowEntries, query, (m, p) => MonsterPredicates.TryMatch(m, p, out var r) && r);
    }
}
