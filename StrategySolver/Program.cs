using System;
using Neo;
using Neo.Network.RPC;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Network.RPC.Models;
using System.Collections.Generic;
using System.Linq;
using Neo.VM.Types;
using System.Numerics;


namespace StrategySolver
{
    public class Program
    {
        static void Main(string[] args)
        {
            int n = GovernanceRetriever.Program.Instance.agents.Count;
            GovernanceRetriever.Program.Instance.filtered.Value.Sort((x, y) => x.Item2.CompareTo(y.Item2));
            List<(byte[], BigInteger)> elected = GovernanceRetriever.Program.Instance.filtered.Value.TakeLast(21).ToList();
            List<(byte[], BigInteger)> weighted = elected.Take(14).Select(v => (v.Item1, v.Item2 * 2)).Concat(elected.TakeLast(7)).ToList();
            weighted.Sort((x, y) => x.Item2.CompareTo(y.Item2));
            List<byte[]> selected = weighted.Take(n).Select(v => v.Item1).ToList();
            List<(BigInteger, BigInteger)> kv = selected.Select(v => (elected.Where(w => w.Item1.SequenceEqual(v)).Single().Item2, (BigInteger)(elected.Take(14).Where(w => w.Item1.SequenceEqual(v)).Any() ? 1 : 2))).ToList();
            // TODO: SOLVE FOR X
            kv.ForEach(v => Console.WriteLine($"{v.Item1}: {v.Item2}"));
        }
    }
}
