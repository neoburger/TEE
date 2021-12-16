﻿using System;
using System.Collections.Generic;
using System.Linq;
using Neo;
using Neo.VM.Types;

namespace LibHelper
{
    public static class Program
    {
        static void Main(string[] args) => "OK!".Log();
        public static bool HasBytes(this IEnumerable<byte[]> list, byte[] val) => list.Where(v => v.SequenceEqual(val)).Any();
        public static T FindBy<T>(this IEnumerable<(byte[], T)> list, byte[] val) => list.Where(v => v.Item1.SequenceEqual(val)).Single().Item2;
        public static void SortBy<T>(this List<T> list, Func<T, IComparable> f) => list.Sort((x, y) => f(x).CompareTo(f(y)));
        public static void Log<T>(this T val) => Console.Error.WriteLine(val);
        public static void Out<T>(this T val) => Console.WriteLine(val);
        public static UInt160 ToU160(this StackItem val) => new UInt160(val.GetSpan());
        public static byte[] ToBytes(this StackItem val) => val.GetSpan().ToArray();
        public static Neo.VM.Types.Array ToVMArray(this StackItem val) => (Neo.VM.Types.Array)val;
        public static Neo.VM.Types.Struct ToVMStruct(this StackItem val) => (Neo.VM.Types.Struct)val;
    }
}
