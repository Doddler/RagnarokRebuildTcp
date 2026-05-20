using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Assets.Scripts.UI.ClientDatabase
{
    public partial class ClientDatabaseWindow
    {
        // Common surface so autocomplete can read keys / value lists without
        // caring about the closed generic parameter.
        internal interface IPredicateRegistry
        {
            string[] Keys { get; }
            bool TryGetValues(string key, out string[] values);
        }

        internal sealed class PredicateRegistry<T> : IPredicateRegistry
        {
            private struct Entry
            {
                public Func<T, SearchPredicate, bool> Handler;
                public string[] CompletionValues;
            }

            private readonly Dictionary<string, Entry> entries = new(StringComparer.OrdinalIgnoreCase);

            public string[] Keys { get; private set; } = Array.Empty<string>();

            public bool TryMatch(T entry, SearchPredicate p, out bool result)
            {
                if (entries.TryGetValue(p.Key, out var e))
                {
                    result = e.Handler(entry, p);
                    return true;
                }
                result = false;
                return false;
            }

            public bool TryGetValues(string key, out string[] values)
            {
                if (entries.TryGetValue(key, out var e) && e.CompletionValues != null)
                {
                    values = e.CompletionValues;
                    return true;
                }
                values = null;
                return false;
            }

            public void Register(
                string key, Func<T, SearchPredicate, bool> handler, string[] completionValues = null)
            {
                entries[key.ToLowerInvariant()] = new Entry
                {
                    Handler = handler,
                    CompletionValues = completionValues,
                };
                Keys = entries.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
            }

            public void SetCompletionValues(string key, string[] values)
            {
                if (!entries.TryGetValue(key, out var e)) return;
                e.CompletionValues = values;
                entries[key.ToLowerInvariant()] = e;
            }
        }

        // Builds a registry by reflecting T's public instance fields and
        // picking a comparator based on the field's type. Uses FieldInfo.GetValue
        // for AOT compatibility (IL2CPP can't JIT Expression.Compile); the cost
        // is one box per value-type read per filter call, paid only on the
        // debounced filter event so the GC churn is minor in practice.
        // Unsupported types (e.g. List<DropEntry>, float) are silently skipped —
        // they just don't become searchable predicates.
        internal static PredicateRegistry<T> BuildPredicateRegistry<T>()
        {
            var reg = new PredicateRegistry<T>();
            foreach (var f in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var key = f.Name.ToLowerInvariant();
                var ft = f.FieldType;
                var field = f;

                if (ft == typeof(int))
                {
                    reg.Register(key, (e, p) => CompareNum((int)field.GetValue(e), p));
                }
                else if (ft == typeof(string))
                {
                    reg.Register(key, (e, p) => CompareStr((string)field.GetValue(e), p));
                }
                else if (ft == typeof(bool))
                {
                    reg.Register(key, (e, p) => CompareBool((bool)field.GetValue(e), p), new[] { "false", "true" });
                }
                else if (ft == typeof(List<string>))
                {
                    reg.Register(key, (e, p) => CompareList((List<string>)field.GetValue(e), p));
                }
                else if (ft.IsEnum)
                {
                    RegisterEnumField(reg, key, field);
                }
            }
            return reg;
        }

        private static void RegisterEnumField<T>(PredicateRegistry<T> reg, string key, FieldInfo f)
        {
            var ft = f.FieldType;
            var enumNames = Enum.GetNames(ft)
                .Select(n => n.ToLowerInvariant())
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToArray();

            if (ft.IsDefined(typeof(FlagsAttribute), false))
            {
                // Flags enum — bitmask match via ComparePosition (which
                // accepts enum member names through Utils.PositionAliases).
                reg.Register(key, (e, p) =>
                {
                    var v = Convert.ToInt32(f.GetValue(e), CultureInfo.InvariantCulture);
                    return ComparePosition(v, p);
                }, enumNames);
            }
            else
            {
                // Pre-build a value→lowercase-name lookup so the per-row
                // comparator never re-allocates a string for the name.
                var nameMap = new Dictionary<int, string>();
                foreach (var name in Enum.GetNames(ft))
                {
                    var v = Convert.ToInt32(Enum.Parse(ft, name), CultureInfo.InvariantCulture);
                    if (!nameMap.ContainsKey(v)) nameMap[v] = name.ToLowerInvariant();
                }
                reg.Register(key, (e, p) =>
                {
                    var v = Convert.ToInt32(f.GetValue(e), CultureInfo.InvariantCulture);
                    if (int.TryParse(p.Value, NumberStyles.Integer,
                            CultureInfo.InvariantCulture, out _))
                        return CompareNum(v, p);
                    var n = nameMap.TryGetValue(v, out var s) ? s : "";
                    return p.Op switch
                    {
                        "=" => n.IndexOf(p.Value, StringComparison.OrdinalIgnoreCase) >= 0,
                        "!=" => n.IndexOf(p.Value, StringComparison.OrdinalIgnoreCase) < 0,
                        _ => false,
                    };
                }, enumNames);
            }
        }
    }
}
