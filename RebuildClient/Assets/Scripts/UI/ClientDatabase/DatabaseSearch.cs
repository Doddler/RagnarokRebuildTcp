using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Scripts.UI.ClientDatabase
{
    internal struct SearchPredicate
    {
        public string Key;
        public string Op;
        public string Value;
    }

    internal static class DatabaseSearch
    {
        private static readonly string[] PredicateOps = { ">=", "<=", "!=", "=", ">", "<" };

        private static string lastQuery;
        private static string lastFreeText;
        private static List<SearchPredicate> lastPredicates;

        public static int Filter<T>(
            IEnumerable<T> source,
            List<T> destination,
            string query,
            Func<T, string> getSearchText,
            Func<T, SearchPredicate, bool> matchesPredicate,
            Predicate<T> include = null,
            Comparison<T> sort = null)
        {
            var (freeText, predicates) = Parse(query);
            destination.Clear();
            var totalCount = 0;

            foreach (var entry in source)
            {
                if (include != null && !include(entry))
                    continue;

                totalCount++;
                if ((freeText.Length == 0 ||
                     getSearchText(entry).IndexOf(freeText, StringComparison.OrdinalIgnoreCase) >= 0) &&
                    MatchesAllPredicates(entry, predicates, matchesPredicate))
                {
                    destination.Add(entry);
                }
            }

            if (sort != null)
                destination.Sort(sort);

            return totalCount;
        }

        private static (string freeText, List<SearchPredicate> predicates) Parse(string query)
        {
            if (query == lastQuery && lastPredicates != null)
                return (lastFreeText, lastPredicates);

            var predicates = new List<SearchPredicate>();
            if (string.IsNullOrWhiteSpace(query))
                return Cache(query, "", predicates);

            string freeText = null;
            List<string> freeParts = null;
            foreach (var token in Tokenize(query))
            {
                if (token.Length >= 2 && token[0] == '#' && TryParsePredicate(token.Substring(1), out var predicate))
                {
                    predicates.Add(predicate);
                    continue;
                }

                if (freeText == null)
                    freeText = token;
                else
                {
                    freeParts ??= new List<string> { freeText };
                    freeParts.Add(token);
                }
            }

            return Cache(query, freeParts != null ? string.Join(" ", freeParts) : freeText ?? "", predicates);
        }

        private static (string freeText, List<SearchPredicate> predicates) Cache(
            string query, string freeText, List<SearchPredicate> predicates)
        {
            lastQuery = query;
            lastFreeText = freeText;
            lastPredicates = predicates;
            return (freeText, predicates);
        }

        private static List<string> Tokenize(string query)
        {
            var tokens = new List<string>();
            var builder = new StringBuilder();
            var inQuote = false;

            for (var i = 0; i < query.Length; i++)
            {
                var character = query[i];
                if (character == '"')
                    inQuote = !inQuote;
                else if (character == ' ' && !inQuote)
                {
                    if (builder.Length == 0)
                        continue;
                    tokens.Add(builder.ToString());
                    builder.Length = 0;
                }
                else
                    builder.Append(character);
            }

            if (builder.Length > 0)
                tokens.Add(builder.ToString());
            return tokens;
        }

        private static bool TryParsePredicate(string body, out SearchPredicate predicate)
        {
            foreach (var operation in PredicateOps)
            {
                var operationIndex = body.IndexOf(operation, StringComparison.Ordinal);
                if (operationIndex <= 0)
                    continue;

                predicate = new SearchPredicate
                {
                    Key = body.Substring(0, operationIndex).ToLowerInvariant(),
                    Op = operation,
                    Value = body.Substring(operationIndex + operation.Length).ToLowerInvariant()
                };
                return true;
            }

            predicate = default;
            return false;
        }

        private static bool MatchesAllPredicates<T>(
            T entry,
            List<SearchPredicate> predicates,
            Func<T, SearchPredicate, bool> matchesPredicate)
        {
            for (var i = 0; i < predicates.Count; i++)
            {
                if (!matchesPredicate(entry, predicates[i]))
                    return false;
            }

            return true;
        }
    }
}
