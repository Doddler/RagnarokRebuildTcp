using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Assets.Scripts.UI.ClientDatabase
{
    public partial class ClientDatabaseWindow
    {
        private string[] monsterElementValues = Array.Empty<string>();
        private string[] monsterRaceValues = Array.Empty<string>();
        private string[] monsterSizeValues = Array.Empty<string>();
        
        private readonly List<string> acSuffixes = new();
        private int acIdx;
        private TMP_InputField acField;
        private string acQuery;
        
        private int acPrevCaret = -1;
        private TMP_InputField acPrevField;

        private void BuildMonsterValueIndex(MonsterDbFile monsterDb)
        {
            if (monsterDb?.Items == null) return;
            var els = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var races = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sizes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in monsterDb.Items)
            {
                if (!string.IsNullOrEmpty(m.Element)) els.Add(m.Element);
                if (!string.IsNullOrEmpty(m.Race)) races.Add(m.Race);
                if (!string.IsNullOrEmpty(m.Size)) sizes.Add(m.Size);
            }
            monsterElementValues = els.Select(s => s.ToLowerInvariant()).OrderBy(s => s).ToArray();
            monsterRaceValues = races.Select(s => s.ToLowerInvariant()).OrderBy(s => s).ToArray();
            monsterSizeValues = sizes.Select(s => s.ToLowerInvariant()).OrderBy(s => s).ToArray();
            
            MonsterPredicates.SetCompletionValues("element", monsterElementValues);
            MonsterPredicates.SetCompletionValues("race", monsterRaceValues);
            MonsterPredicates.SetCompletionValues("size", monsterSizeValues);
        }

        private IPredicateRegistry RegistryForField(TMP_InputField field)
        {
            if (field == monsterSearchField) return MonsterPredicates;
            if (field == itemSearchField) return ItemPredicates;
            if (field == mapSearchField) return MapPredicates;
            return null;
        }

        private TextMeshProUGUI GhostForField(TMP_InputField field)
        {
            if (field == monsterSearchField) return monsterSearchGhost;
            if (field == itemSearchField) return itemSearchGhost;
            if (field == mapSearchField) return mapSearchGhost;
            return null;
        }
        
        private void ComputeCompletions(
            string query, int cursorPos, IPredicateRegistry reg, List<string> results)
        {
            results.Clear();
            if (string.IsNullOrEmpty(query) || cursorPos != query.Length || reg == null) return;

            var lastSpace = query.LastIndexOf(' ', cursorPos - 1);
            var word = lastSpace < 0 ? query : query.Substring(lastSpace + 1);
            if (word.Length == 0 || word[0] != '#') return;

            var body = word.Substring(1);

            var opIdx = -1;
            for (var i = 0; i < body.Length; i++)
            {
                var c = body[i];
                if (c == '=' || c == '<' || c == '>' || c == '!') { opIdx = i; break; }
            }

            if (opIdx < 0)
            {
                CollectMatching(reg.Keys, body, results);
                return;
            }

            string op;
            int valueStart;
            if (opIdx + 1 < body.Length)
            {
                var two = body.Substring(opIdx, 2);
                if (two == "<=" || two == ">=" || two == "!=") { op = two; valueStart = opIdx + 2; }
                else { op = body.Substring(opIdx, 1); valueStart = opIdx + 1; }
            }
            else { op = body.Substring(opIdx, 1); valueStart = opIdx + 1; }

            if (op != "=" && op != "!=") return;

            var key = body.Substring(0, opIdx).ToLowerInvariant();
            if (!reg.TryGetValues(key, out var values)) return;
            CollectMatching(values, body.Substring(valueStart), results);
        }

        private static void CollectMatching(string[] options, string prefix, List<string> results)
        {
            foreach (var opt in options)
            {
                if (opt.Length > prefix.Length
                    && opt.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(opt.Substring(prefix.Length));
                }
            }
        }

        private void RefreshAutocompleteFor(TMP_InputField field)
        {
            var reg = RegistryForField(field);
            var ghost = GhostForField(field);
            if (reg == null || ghost == null) return;

            var query = field.text ?? "";
            if (acField != field || acQuery != query)
            {
                ComputeCompletions(query, field.caretPosition, reg, acSuffixes);
                acIdx = 0;
                acField = field;
                acQuery = query;
            }

            DrawGhost(field, ghost);
        }

        private void DrawGhost(TMP_InputField field, TextMeshProUGUI ghost)
        {
            if (acSuffixes.Count == 0)
            {
                if (ghost.text.Length != 0) ghost.text = "";
                return;
            }

            var query = field.text ?? "";
            var suffix = acSuffixes[acIdx];
            
            ghost.text =
                $"<color=#00000000><noparse>{query}</noparse></color>" +
                $"<color=#888888><noparse>{suffix}</noparse></color>";
        }

        private void ClearAutocompleteState(TMP_InputField field)
        {
            var ghost = GhostForField(field);
            if (ghost != null && ghost.text.Length != 0) ghost.text = "";
            if (acField == field)
            {
                acSuffixes.Clear();
                acField = null;
                acQuery = null;
                acIdx = 0;
            }
        }

        private void WireAutocompleteGhosts()
        {
            HookGhost(monsterSearchField);
            HookGhost(itemSearchField);
            HookGhost(mapSearchField);
        }

        private void HookGhost(TMP_InputField field)
        {
            if (field == null) return;
            var nav = field.navigation;
            nav.mode = UnityEngine.UI.Navigation.Mode.None;
            field.navigation = nav;

            field.onValueChanged.AddListener(_ => RefreshAutocompleteFor(field));
            field.onDeselect.AddListener(_ => ClearAutocompleteState(field));
        }

        private void HandleAutocompleteInput()
        {
            TMP_InputField field;
            if (monsterSearchField != null && monsterSearchField.isFocused) field = monsterSearchField;
            else if (itemSearchField != null && itemSearchField.isFocused) field = itemSearchField;
            else if (mapSearchField != null && mapSearchField.isFocused) field = mapSearchField;
            else { acPrevField = null; acPrevCaret = -1; return; }

            if (acField != field) RefreshAutocompleteFor(field);

            var ghost = GhostForField(field);
            var hasSuggestion = acField == field && acSuffixes.Count > 0;

            if (Input.GetKeyDown(KeyCode.DownArrow) && hasSuggestion)
            {
                acIdx = (acIdx + 1) % acSuffixes.Count;
                if (ghost != null) DrawGhost(field, ghost);
            }
            else if (Input.GetKeyDown(KeyCode.UpArrow) && hasSuggestion)
            {
                acIdx = (acIdx - 1 + acSuffixes.Count) % acSuffixes.Count;
                if (ghost != null) DrawGhost(field, ghost);
            }
            else if (Input.GetKeyDown(KeyCode.Tab) && hasSuggestion)
            {
                AcceptCurrentSuggestion(field, ghost);
            }
            else if (Input.GetKeyDown(KeyCode.RightArrow) && hasSuggestion)
            {
                var len = (field.text ?? "").Length;
                if (acPrevField == field && acPrevCaret == len && field.caretPosition == len)
                    AcceptCurrentSuggestion(field, ghost);
            }

            acPrevField = field;
            acPrevCaret = field.caretPosition;
        }

        private void AcceptCurrentSuggestion(TMP_InputField field, TextMeshProUGUI ghost)
        {
            var suffix = acSuffixes[acIdx];
            var query = field.text ?? "";
            field.text = query + suffix;
            field.caretPosition = field.text.Length;
            field.MoveTextEnd(false);
            if (ghost != null) ghost.text = "";
        }
    }
}
