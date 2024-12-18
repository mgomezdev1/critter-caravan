using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Extensions
{
    public static class CollectionsUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Swap<T>(this IList<T> list, System.Index indexA, System.Index indexB)
        {
            (list[indexB], list[indexA]) = (list[indexA], list[indexB]);
        }

        public static int Count<T>(this IEnumerable<T> enumerable, T item)
        {
            int count = 0;
            foreach (var itemA in enumerable)
            {
                if (itemA.Equals(item)) count++;
            }
            return count;
        }

        public static Dictionary<T, int> Counts<T>(this IEnumerable<T> enumerable)
        {
            Dictionary<T, int> result = new();
            foreach (var item in enumerable)
            {
                if (result.ContainsKey(item)) result[item]++;
                else result[item] = 1;
            }
            return result;
        }

        public static IEnumerable<List<T>> InSetsOf<T>(this IEnumerable<T> data, int maxSetSize)
        {
            List<T> group = new();
            foreach (var item in data)
            {
                group.Add(item);
                if (group.Count > maxSetSize)
                {
                    yield return group;
                    group = new();
                }
            }
            if (group.Count > 0)
            {
                yield return group;
            }
        }
    }
}