using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Cardie.Support
{
    public static class Extensions
    {
        public static void AddRange<T>(this IList<T> list, IEnumerable<T> values)
        {
            foreach (var value in values)
                list.Add(value);
        }

        public static void Shuffle<T>(this IList<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = ThreadSafeRandom.ThisThreadsRandom.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            foreach (var value in source)
                action(value);
        }

        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source) => new(source);
    }
}
