using System;
using System.Collections.Generic;
using System.Linq;
using Neo;
using Neo.VM.Types;

namespace LibHelper
{
    public static class Program
    {
        static void Main(string[] args) => "OK!".Log();
        public static (List<S>, List<T>) Map2<R, S, T>(this IEnumerable<R> list, Func<R, S> fx, Func<R, T> fy) => (list.Select(v => fx(v)).ToList(), list.Select(v => fy(v)).ToList());
        public static bool HasBytes(this IEnumerable<byte[]> list, byte[] val) => list.Where(v => v.SequenceEqual(val)).Any();
        public static T FindBy<T>(this IEnumerable<(byte[], T)> list, byte[] val) => list.Where(v => v.Item1.SequenceEqual(val)).Single().Item2;
        public static T FindByOrDefault<T>(this IEnumerable<(byte[], T)> list, byte[] val) => list.Where(v => v.Item1.SequenceEqual(val)).SingleOrDefault().Item2;
        public static IEnumerable<T> Merge<T>(this IEnumerable<T> list, IEnumerable<T> other) => list.Merge(new Queue<T>(other), v => v is null);
        public static IEnumerable<T> Merge<T>(this IEnumerable<T> list, IEnumerable<T> other, Func<T, bool> f) => list.Merge(new Queue<T>(other), f);
        public static IEnumerable<T> Merge<T>(this IEnumerable<T> list, Queue<T> other, Func<T, bool> f) => list.Select(v => f(v) ? other.Dequeue() : v);

        public static void Log<T>(this T val) => Console.Error.WriteLine(val);
        public static void Out<T>(this T val) => Console.WriteLine(val);
        public static UInt160 ToU160(this StackItem val) => new UInt160(val.GetSpan());
        public static byte[] ToBytes(this StackItem val) => val.GetSpan().ToArray();
        public static IEnumerable<T> Nullize<T>(this IEnumerable<T> val) => val.Any() ? val : null;
        public static Neo.VM.Types.Array ToVMArray(this StackItem val) => (Neo.VM.Types.Array)val;
        public static Neo.VM.Types.Struct ToVMStruct(this StackItem val) => (Neo.VM.Types.Struct)val;
        public static void Assert(this bool val) { if (val == false) throw new Exception(); }
    }
}
