using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
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
        private static readonly Color s_activeTabColor = Color.white;
        private static readonly Color s_inactiveTabColor = new(0.78f, 0.78f, 0.80f, 1f);
        private static readonly Color s_defaultTextColor = new(0.08f, 0.08f, 0.10f, 1f);

        private static string FormatItemName(ItemData item) => item.Slots > 0 ? $"{item.Name}[{item.Slots}]" : item.Name;
        private static string FormatNameWithBracket(string name, string bracket) => $"{name}  <size=80%><color=#888888>[{bracket}]</color></size>";
        private static readonly Regex LineHeightTagRegex = new(@"</?line-height(=[^>]*)?>", RegexOptions.Compiled);
        private static string SanitizeRichText(string raw) => LineHeightTagRegex.Replace(raw, string.Empty);
        private static Sprite GetItemIcon(string spriteName) => string.IsNullOrEmpty(spriteName) || ClientDataLoader.Instance == null ? null : ClientDataLoader.Instance.GetIconAtlasSprite(spriteName);
        
        internal struct SearchPredicate
        {
            public string Key;
            public string Op;
            public string Value;
        }
        
        private static readonly string[] PredicateOps = { ">=", "<=", "!=", "=", ">", "<" };
        
        private static string lastParseQueryInput;
        private static string lastParseQueryFree;
        private static List<SearchPredicate> lastParseQueryPreds;

        private static (string freeText, List<SearchPredicate> predicates) ParseQuery(string query)
        {
            if (query == lastParseQueryInput && lastParseQueryPreds != null) return (lastParseQueryFree, lastParseQueryPreds);

            var predicates = new List<SearchPredicate>();
            if (string.IsNullOrWhiteSpace(query))
            {
                lastParseQueryInput = query;
                lastParseQueryFree = "";
                lastParseQueryPreds = predicates;
                return ("", predicates);
            }

            string free = null;
            List<string> freeParts = null;
            foreach (var token in TokenizeQuery(query))
            {
                if (token.Length >= 2 && token[0] == '#' && TryParsePredicate(token.Substring(1), out var p))
                {
                    predicates.Add(p);
                }
                else
                {
                    if (free == null) free = token;
                    else
                    {
                        if (freeParts == null) freeParts = new List<string> { free };
                        freeParts.Add(token);
                    }
                }
            }
            var freeText = freeParts != null ? string.Join(" ", freeParts) : (free ?? "");

            lastParseQueryInput = query;
            lastParseQueryFree = freeText;
            lastParseQueryPreds = predicates;
            return (freeText, predicates);
        }
        
        private static List<string> TokenizeQuery(string query)
        {
            var tokens = new List<string>();
            var sb = new StringBuilder();
            var inQuote = false;
            for (var i = 0; i < query.Length; i++)
            {
                var c = query[i];
                if (c == '"')
                {
                    inQuote = !inQuote;
                }
                else if (c == ' ' && !inQuote)
                {
                    if (sb.Length > 0) { tokens.Add(sb.ToString()); sb.Length = 0; }
                }
                else
                {
                    sb.Append(c);
                }
            }
            if (sb.Length > 0) tokens.Add(sb.ToString());
            return tokens;
        }

        private static bool TryParsePredicate(string body, out SearchPredicate pred)
        {
            foreach (var op in PredicateOps)
            {
                var idx = body.IndexOf(op, StringComparison.Ordinal);
                if (idx <= 0) continue;
                pred = new SearchPredicate
                {
                    Key = body.Substring(0, idx).ToLowerInvariant(),
                    Op = op,
                    Value = body.Substring(idx + op.Length).ToLowerInvariant(),
                };
                return true;
            }
            pred = default;
            return false;
        }

        private static void ApplyFilter<T>(List<(GameObject go, T entry, string searchText)> rows, string query, Func<T, SearchPredicate, bool> matchPredicate)
        {
            var (free, preds) = ParseQuery(query);
            var hasFree = free.Length > 0;
            var predCount = preds.Count;
            for (var i = 0; i < rows.Count; i++)
            {
                var (go, entry, text) = rows[i];
                var match = (!hasFree || text.IndexOf(free, StringComparison.OrdinalIgnoreCase) >= 0) && AllPredicatesMatch(entry, preds, predCount, matchPredicate);
                
                if (go.activeSelf != match)
                    go.SetActive(match);
            }
        }

        private static bool AllPredicatesMatch<T>(T entry, List<SearchPredicate> preds, int predCount, Func<T, SearchPredicate, bool> matchPredicate)
        {
            for (var i = 0; i < predCount; i++)
            {
                if (!matchPredicate(entry, preds[i])) return false;
            }
            return true;
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
            var path = Path.Combine(Application.streamingAssetsPath, relativePath);
            if (!File.Exists(path))
            {
                Debug.LogError($"Streaming file not found: {path}");
                return null;
            }
            return File.ReadAllText(path);
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

        private static void ClearChildren(Transform t)
        {
            for (var i = t.childCount - 1; i >= 0; i--)
                Destroy(t.GetChild(i).gameObject);
        }

        private void CreateInfoRow(Transform parent, string text)
        {
            var row = CreateLabel("None", parent, text, 12);
            row.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.MidlineLeft;
            row.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 20);
        }
        
        private GameObject CloneRow(GameObject template, Transform parent, float height, bool clickable = true)
        {
            var row = Instantiate(template, parent);
            row.SetActive(true);
            ((RectTransform)row.transform).sizeDelta = new Vector2(0, height);
            if (!clickable)
                row.GetComponent<Button>().interactable = false;
            return row;
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
