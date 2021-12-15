using System;
using System.Collections.Generic;
using System.Linq;
using Neo;

namespace LibHelper
{
    public static class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("OK!");
        }
        public static bool HasBytes(this IEnumerable<byte[]> list, byte[] val) => list.Where(v => v.SequenceEqual(val)).Any();
        public static bool HasBytes<T>(this IEnumerable<(byte[], T)> list, byte[] val) => list.Select(v => v.Item1).HasBytes(val);
        public static T FindBytes<T>(this IEnumerable<(byte[], T)> list, byte[] val) => list.Where(v => v.Item1.SequenceEqual(val)).Single().Item2;
        public static void SortBy<T>(this List<T> list, Func<T, IComparable> f) => list.Sort((x, y) => f(x).CompareTo(f(y)));
        public static void Log<T>(this T val) => Console.Error.WriteLine(val);
        public static void Out<T>(this T val) => Console.WriteLine(val);
        public static readonly UInt160 BNEO = UInt160.Parse("0x48c40d4666f93408be1bef038b6722404d9a4c2a");
    }
}
