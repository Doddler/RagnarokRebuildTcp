using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Assets.Scripts.Network;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI.ClientDatabase
{
    public partial class ClientDatabaseWindow
    {
        private float frameTimer;
        private int monsterFrame;
        private int currentMonsterDetailId = -1;

        private IReadOnlyList<MonsterEntry> monsterEntries = Array.Empty<MonsterEntry>();
        private readonly List<MonsterEntry> filteredMonsters = new();

        private const string MonsterSpriteBasePath = "Assets/Sprites/Monsters/";

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

        [SerializeField, HideInInspector] internal TextMeshProUGUI monsterSearchGhost;
        [SerializeField, HideInInspector] internal TextMeshProUGUI monsterDetailStatsText;
        [SerializeField, HideInInspector] internal UiPlayerSprite monsterSprite;

        private void PopulateMonsterList(MonsterDbFile data)
        {
            monsterEntries = data?.Items ?? (IReadOnlyList<MonsterEntry>)Array.Empty<MonsterEntry>();
            monsterPage.Refresh();
        }

        private void BindMonsterListRow(DatabaseListRow row, MonsterEntry m, int index)
        {
            row.SetLabel($"{m.Name}  (Lv {m.Level})");
            row.SetIcon(null);
            row.SetActions(
                () => ShowMonsterDetail(m),
                () => NetworkManager.Instance.SendAdminSummonMonster(m.Code, 1));
        }

        private void ShowMonsterDetail(MonsterEntry m)
        {
            monsterPage.ShowDetail();
            currentMonsterDetailId = m.Id;
            monsterPage.DetailTitleText.text = FormatNameWithBracket(m.Name, m.Id.ToString());
            
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

            ReleaseDetailRows(monsterPage.SecondaryContent);
            if (m.Drops == null || m.Drops.Count == 0)
            {
                CreateInfoRow(monsterPage.SecondaryContent, "(no drops)");
            }
            else
            {
                foreach (var d in m.Drops.OrderByDescending(d => d.Chance))
                    CreateDropRow(d);
            }

            ReleaseDetailRows(monsterPage.TertiaryContent);
            if (m.Spawns == null || m.Spawns.Count == 0)
            {
                CreateInfoRow(monsterPage.TertiaryContent, "(no live spawns)");
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

            var row = GetDetailRow(rowTemplate, monsterPage.SecondaryContent, 26, clickable: hasItem);
            row.GetComponentInChildren<TextMeshProUGUI>(true).text = $"{name}  —  {pct:0.##}%";
            SetRowIcon(row, hasItem ? GetItemIcon(item.Sprite) : null);

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

            var row = GetDetailRow(rowTemplate, monsterPage.TertiaryContent, 22, clickable: hasMap);
            row.GetComponentInChildren<TextMeshProUGUI>(true).text = $"{FormatMapLabel(s.Map)}  —  {s.Count}";
            if (hasMap)
            {
                var captured = map;
                row.GetComponent<Button>().onClick.AddListener(() => JumpToMap(captured));
                AttachRightClick(row, () => NetworkManager.Instance.SendMoveRequest(captured.Code));
            }
        }

        private void LoadMonsterSprite(int monsterId)
        {
            monsterSprite.Clear();
            monsterSprite.gameObject.SetActive(false);
            monsterFrame = 0;
            frameTimer = 0f;

            if (!ClassLookup.TryGetValue(monsterId, out var cls) || string.IsNullOrEmpty(cls.SpriteName))
            {
                Debug.LogWarning($"No sprite name available for monster {monsterId}");
                return;
            }

            var path = MonsterSpriteBasePath + cls.SpriteName;
            monsterSprite.gameObject.SetActive(true);
            monsterSprite.DisplaySprite(path);
        }

        public void ReturnToMonsterList()
        {
            monsterPage.ShowList();
            monsterSprite.Clear();
            monsterSprite.gameObject.SetActive(false);
            monsterFrame = 0;
        }
        
        internal static readonly PredicateRegistry<MonsterEntry> MonsterPredicates = BuildPredicateRegistry<MonsterEntry>();

        private void FilterMonsters(string query) =>
            ApplyDatabaseFilter<MonsterEntry, DatabaseListRow>(
                monsterEntries,
                filteredMonsters,
                query,
                monsterPage.VirtualList,
                nameof(monsterPage),
                "Monsters",
                monsterPage.TitleText,
                BindMonsterListRow,
                static monster => $"{monster.Id} {monster.Name} {monster.Code}",
                MonsterPredicates);
    }
}
