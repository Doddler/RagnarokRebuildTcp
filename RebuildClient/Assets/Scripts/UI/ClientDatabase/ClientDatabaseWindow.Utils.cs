using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Assets.Scripts.Sprites;
using Assets.Scripts.UI.Utility;
using RebuildSharedData.ClientTypes;
using RebuildSharedData.Enum;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Assets.Scripts.UI.ClientDatabase
{
    public partial class ClientDatabaseWindow
    {
        private static readonly Color s_defaultTextColor = new(0.08f, 0.08f, 0.10f, 1f);

        private static string FormatItemName(ItemData item) => item.Slots > 0 ? $"{item.Name}[{item.Slots}]" : item.Name;
        private static string FormatNameWithBracket(string name, string bracket) => $"{name}  <size=80%><color=#888888>[{bracket}]</color></size>";
        private static readonly Regex LineHeightTagRegex = new(@"</?line-height(=[^>]*)?>", RegexOptions.Compiled);
        private static string SanitizeRichText(string raw) => LineHeightTagRegex.Replace(raw, string.Empty);
        private static Sprite GetItemIcon(string spriteName) => string.IsNullOrEmpty(spriteName) || ClientDataLoader.Instance == null ? null : ClientDataLoader.Instance.GetIconAtlasSprite(spriteName);
        
        private void ApplyDatabaseFilter<TData, TRow>(
            IEnumerable<TData> source,
            List<TData> filtered,
            string query,
            VirtualList virtualList,
            string fieldName,
            string title,
            TextMeshProUGUI titleText,
            Action<TRow, TData, int> bindItem,
            Func<TData, string> getSearchText,
            PredicateRegistry<TData> predicates,
            Predicate<TData> include = null,
            Comparison<TData> sort = null)
            where TRow : Component
        {
            if (virtualList == null)
                throw new InvalidOperationException(
                    $"{nameof(ClientDatabaseWindow)} on {name} is missing required inspector reference {fieldName}.");

            var totalCount = DatabaseSearch.Filter(
                source,
                filtered,
                query,
                getSearchText,
                (entry, predicate) =>
                    predicates.TryMatch(entry, predicate, out var result) && result,
                include,
                sort);

            virtualList.SetItems(filtered, bindItem);
            titleText.text = filtered.Count == totalCount
                ? $"{title} ({totalCount})"
                : $"{title} ({filtered.Count}/{totalCount})";
        }

        private static bool CompareNum(int actual, SearchPredicate p)
        {
            if (!int.TryParse(p.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var target)) return false;
            return p.Op switch
            {
                "=" => actual == target,
                "!=" => actual != target,
                "<" => actual < target,
                "<=" => actual <= target,
                ">" => actual > target,
                ">=" => actual >= target,
                _ => false,
            };
        }

        private static bool CompareStr(string actual, SearchPredicate p)
        {
            var idx = (actual ?? "").IndexOf(p.Value, StringComparison.OrdinalIgnoreCase);
            return p.Op switch
            {
                "=" => idx >= 0,
                "!=" => idx < 0,
                _ => false,
            };
        }
        
        private static bool CompareBool(bool actual, SearchPredicate p)
        {
            bool target;
            switch (p.Value)
            {
                case "true": case "yes": case "1": target = true; break;
                case "false": case "no": case "0": target = false; break;
                default: return false;
            }
            return p.Op switch
            {
                "=" => actual == target,
                "!=" => actual != target,
                _ => false,
            };
        }
        
        private static readonly Dictionary<string, int> PositionAliases = BuildPositionAliases();

        private static Dictionary<string, int> BuildPositionAliases()
        {
            var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in Enum.GetNames(typeof(EquipPosition)))
            {
                var value = (int)(EquipPosition)Enum.Parse(typeof(EquipPosition), name);
                dict[name] = value;
            }
            return dict;
        }

        private static bool ComparePosition(int posMask, SearchPredicate p)
        {
            int target;
            
            if (int.TryParse(p.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                target = n;
            else if (!PositionAliases.TryGetValue(p.Value, out target))
                return false;
            
            var hit = target == 0 ? posMask == 0 : (posMask & target) != 0;
            return p.Op switch
            {
                "=" => hit,
                "!=" => !hit,
                _ => false,
            };
        }

        private static bool CompareList(List<string> actual, SearchPredicate p)
        {
            var any = false;
            if (actual != null)
            {
                for (var i = 0; i < actual.Count && !any; i++)
                    any = (actual[i] ?? "").IndexOf(p.Value, StringComparison.OrdinalIgnoreCase) >= 0;
            }
            return p.Op switch
            {
                "=" => any,
                "!=" => !any,
                _ => false,
            };
        }

        private static string LoadStreamingFile(string relativePath)
        {
            var json = ClientDataLoader.ReadStreamingAssetFile(relativePath);
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError($"Streaming file not found: {relativePath}");
                return null;
            }
            return json;
        }

        private static T LoadStreamingJson<T>(string relativePath) where T : class
        {
            var json = LoadStreamingFile(relativePath);
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch (Exception e)
            {
                Debug.LogError($"Bad {relativePath}: {e.Message}");
                return null;
            }
        }

        private static string FormatMapLabel(string mapCode, bool withBracket = true)
        {
            if (!MapLookup.TryGetValue(mapCode, out var data) || string.IsNullOrEmpty(data.Name))
                return mapCode;
            return withBracket ? FormatNameWithBracket(data.Name, mapCode) : data.Name;
        }

        private static void SetRowIcon(GameObject row, Sprite sprite)
        {
            row.GetComponent<DatabaseListRow>().SetIcon(sprite);
        }

        internal static GameObject NewUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        internal GameObject CreateLabel(string name, Transform parent, string text, int fontSize)
        {
            var go = NewUIObject(name, parent);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            if (TmpFont != null) t.font = TmpFont;
            if (flatTmpMaterial != null) t.fontSharedMaterial = flatTmpMaterial;
            t.fontSize = fontSize;
            t.color = s_defaultTextColor;
            t.raycastTarget = false;
            return go;
        }

        private static void ReleaseDetailRows(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                if (child.TryGetComponent<ClientDatabasePooledObject>(out _))
                {
                    ResetPooledObject(child);
                    child.SetActive(false);
                }
                else
                    Destroy(child);
            }
        }

        private void CreateInfoRow(Transform parent, string text)
        {
            var row = GetPooledObject(null, parent, () => CreateLabel("None", parent, "", 12));
            var label = row.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            SetDetailRowHeight(row, 20f);
        }

        private GameObject GetDetailRow(GameObject template, Transform parent, float height, bool clickable = true)
        {
            var row = GetPooledObject(template, parent, () => Instantiate(template, parent));
            ResetPooledObject(row);
            SetDetailRowHeight(row, height);
            row.GetComponent<Button>().interactable = clickable;
            return row;
        }

        private static void SetDetailRowHeight(GameObject row, float height)
        {
            ((RectTransform)row.transform).sizeDelta = new Vector2(0f, height);

            var layout = row.GetComponent<LayoutElement>();
            if (layout == null)
                layout = row.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
            layout.flexibleHeight = 0f;
        }

        private static GameObject GetPooledObject(
            GameObject template, Transform parent, Func<GameObject> create)
        {
            for (var i = 0; i < parent.childCount; i++)
            {
                var candidate = parent.GetChild(i).gameObject;
                if (candidate.activeSelf ||
                    !candidate.TryGetComponent<ClientDatabasePooledObject>(out var pooled) ||
                    pooled.Template != template)
                {
                    continue;
                }

                candidate.transform.SetAsLastSibling();
                ResetPooledObject(candidate);
                candidate.SetActive(true);
                return candidate;
            }

            var created = create();
            created.AddComponent<ClientDatabasePooledObject>().Initialize(template);
            created.SetActive(true);
            return created;
        }

        private static void ResetPooledObject(GameObject pooledObject)
        {
            if (pooledObject.TryGetComponent<DatabaseListRow>(out var row))
                row.SetIcon(null);

            if (pooledObject.TryGetComponent<Button>(out var button))
            {
                button.onClick.RemoveAllListeners();
                button.interactable = true;
            }

            var rightClick = pooledObject.GetComponent<ButtonRightClickEventHandler>();
            rightClick?.OnRightClick?.RemoveAllListeners();
        }

        private static void AttachRightClick(GameObject go, UnityAction action)
        {
            var rch = go.GetComponent<ButtonRightClickEventHandler>();
            if (rch == null) rch = go.AddComponent<ButtonRightClickEventHandler>();
            rch.OnRightClick ??= new UnityEvent();
            rch.OnRightClick.AddListener(action);
        }

    }
}
