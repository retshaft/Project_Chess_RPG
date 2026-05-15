using System;
using System.Collections.Generic;

namespace CheckmateRPG.Core.Determinism
{
    /// <summary>
    /// Provides helpers that produce explicit, stable orderings for common simulation
    /// collection types, eliminating any dependency on hash-bucket or insertion order.
    /// <para>
    /// All methods return a newly allocated array so that callers can iterate without
    /// mutating the source collection.
    /// </para>
    /// </summary>
    public static class DeterministicOrderUtility
    {
        // -----------------------------------------------------------------------
        // Dictionary helpers
        // -----------------------------------------------------------------------

        /// <summary>
        /// Returns the values of <paramref name="source"/> sorted by a stable key selector.
        /// </summary>
        public static TValue[] ToSortedValues<TKey, TValue>(
            IReadOnlyDictionary<TKey, TValue> source,
            Comparison<TValue> comparison)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (comparison == null) throw new ArgumentNullException(nameof(comparison));

            TValue[] array = new TValue[source.Count];
            int index = 0;
            foreach (TValue value in source.Values)
                array[index++] = value;

            Array.Sort(array, comparison);
            return array;
        }

        /// <summary>
        /// Returns the key-value pairs of <paramref name="source"/> sorted by a stable
        /// key selector applied to each value.
        /// </summary>
        public static KeyValuePair<TKey, TValue>[] ToSortedPairs<TKey, TValue>(
            IReadOnlyDictionary<TKey, TValue> source,
            Comparison<KeyValuePair<TKey, TValue>> comparison)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (comparison == null) throw new ArgumentNullException(nameof(comparison));

            KeyValuePair<TKey, TValue>[] array = new KeyValuePair<TKey, TValue>[source.Count];
            int index = 0;
            foreach (KeyValuePair<TKey, TValue> pair in source)
                array[index++] = pair;

            Array.Sort(array, comparison);
            return array;
        }

        // -----------------------------------------------------------------------
        // Guid-keyed dictionary helpers (most common case in simulation code)
        // -----------------------------------------------------------------------

        /// <summary>
        /// Returns the values of a <c>Guid</c>-keyed dictionary sorted by the values'
        /// Guid keys in ascending byte order, providing a canonical stable order.
        /// </summary>
        public static TValue[] ToSortedByGuidKey<TValue>(
            IReadOnlyDictionary<Guid, TValue> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            KeyValuePair<Guid, TValue>[] pairs = new KeyValuePair<Guid, TValue>[source.Count];
            int index = 0;
            foreach (KeyValuePair<Guid, TValue> pair in source)
                pairs[index++] = pair;

            Array.Sort(pairs, static (a, b) => a.Key.CompareTo(b.Key));

            TValue[] result = new TValue[pairs.Length];
            for (int i = 0; i < pairs.Length; i++)
                result[i] = pairs[i].Value;

            return result;
        }

        // -----------------------------------------------------------------------
        // HashSet helpers
        // -----------------------------------------------------------------------

        /// <summary>
        /// Returns the elements of <paramref name="source"/> sorted by the provided
        /// <paramref name="comparison"/>, removing the non-deterministic hash iteration order.
        /// </summary>
        public static T[] ToSortedArray<T>(IEnumerable<T> source, Comparison<T> comparison)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (comparison == null) throw new ArgumentNullException(nameof(comparison));

            List<T> list = new List<T>(source);
            list.Sort(comparison);
            return list.ToArray();
        }

        /// <summary>
        /// Returns the elements of a <c>Guid</c> set sorted in ascending byte order.
        /// </summary>
        public static Guid[] ToSortedGuids(IEnumerable<Guid> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            List<Guid> list = new List<Guid>(source);
            list.Sort();
            return list.ToArray();
        }

        // -----------------------------------------------------------------------
        // String-keyed helpers (effects use string keys)
        // -----------------------------------------------------------------------

        /// <summary>
        /// Returns the values of a <c>string</c>-keyed dictionary sorted by key using
        /// ordinal comparison, which is stable and platform-independent.
        /// </summary>
        public static TValue[] ToSortedByStringKey<TValue>(
            IReadOnlyDictionary<string, TValue> source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));

            KeyValuePair<string, TValue>[] pairs = new KeyValuePair<string, TValue>[source.Count];
            int index = 0;
            foreach (KeyValuePair<string, TValue> pair in source)
                pairs[index++] = pair;

            Array.Sort(pairs, static (a, b) =>
                string.Compare(a.Key, b.Key, StringComparison.Ordinal));

            TValue[] result = new TValue[pairs.Length];
            for (int i = 0; i < pairs.Length; i++)
                result[i] = pairs[i].Value;

            return result;
        }

        // -----------------------------------------------------------------------
        // List stable-sort (in-place)
        // -----------------------------------------------------------------------

        /// <summary>
        /// Performs a stable in-place sort of <paramref name="list"/> using the supplied
        /// <paramref name="comparison"/>. Unlike <see cref="List{T}.Sort(Comparison{T})"/>,
        /// this preserves the original relative order of equal elements.
        /// </summary>
        public static void StableSort<T>(List<T> list, Comparison<T> comparison)
        {
            if (list == null) throw new ArgumentNullException(nameof(list));
            if (comparison == null) throw new ArgumentNullException(nameof(comparison));

            // Decorate each element with its original index so equal elements can be
            // resolved by insertion order (which is itself deterministic).
            (T item, int originalIndex)[] decorated =
                new (T, int)[list.Count];

            for (int i = 0; i < list.Count; i++)
                decorated[i] = (list[i], i);

            Array.Sort(decorated, (a, b) =>
            {
                int cmp = comparison(a.item, b.item);
                return cmp != 0 ? cmp : a.originalIndex.CompareTo(b.originalIndex);
            });

            for (int i = 0; i < list.Count; i++)
                list[i] = decorated[i].item;
        }

        /// <summary>
        /// Performs a stable in-place sort of <paramref name="list"/> using
        /// <paramref name="comparer"/>.
        /// </summary>
        public static void StableSort<T>(List<T> list, IComparer<T> comparer)
        {
            if (comparer == null) throw new ArgumentNullException(nameof(comparer));
            StableSort(list, comparer.Compare);
        }
    }
}
