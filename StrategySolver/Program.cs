using System;
using Neo;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;


namespace StrategySolver
{
    public class Program
    {
        public List<byte[]> selected;
        public List<BigInteger> all;
        public List<BigInteger> votes;
        public static Program Instance = new();
        static void Main(string[] args)
        {
            Instance.selected.Zip(Instance.votes).ToList().ForEach(v => Console.WriteLine($"{v.First.ToHexString()}: {v.Second}"));
        }
        private Program()
        {
            int n = GovernanceRetriever.Program.Instance.agents.Count;
            GovernanceRetriever.Program.Instance.filtered.Value.Sort((x, y) => x.Item2.CompareTo(y.Item2));
            List<(byte[], BigInteger)> elected = GovernanceRetriever.Program.Instance.filtered.Value.TakeLast(21).ToList();
            List<(byte[], BigInteger)> weighted = elected.Take(14).Select(v => (v.Item1, v.Item2 * 2)).Concat(elected.TakeLast(7)).ToList();
            weighted.Sort((x, y) => x.Item2.CompareTo(y.Item2));
            selected = weighted.Take(n).Select(v => v.Item1).ToList();
            List<(double, double)> kv = selected.Select(v => ((elected.Take(14).Where(w => w.Item1.SequenceEqual(v)).Any() ? 1.0 : 2.0), ((double)elected.Where(w => w.Item1.SequenceEqual(v)).Single().Item2))).ToList();
            double total = ((double)GovernanceRetriever.Program.Instance.neo.Value) + kv.Select(v => v.Item2).Sum();
            List<double> rates = kv.Select(v => Math.Sqrt(v.Item1 * v.Item2)).ToList();
            double unit = total / rates.Sum();
            all = rates.Select(v => v * unit).Select(v => (BigInteger)v).ToList();
            votes = all.Zip(kv.Select(v => (BigInteger)v.Item2)).Select(v => v.First - v.Second).ToList();
            votes[0] += GovernanceRetriever.Program.Instance.neo.Value - votes.Sum();
            if (votes.Where(v => v < 0).Any()) throw new Exception("negtive vote found");
            
            var rpbpn_old = GovernanceRetriever.Program.Instance.agentstates.Select(v => (v.Item1, GovernanceRetriever.Program.Instance.filtered.Value.Where(w => w.Item1.SequenceEqual(v.Item1)).Single().Item2, v.Item2, elected.Take(14).Select(v => (v.Item1, BigInteger.Parse("1"))).Concat(elected.TakeLast(7).Select(v => (v.Item1, BigInteger.Parse("2"))) ).Where(w => w.Item1.SequenceEqual(v.Item1)).Single().Item2 )).Select(v => BigInteger.Parse("100000000") * v.Item4*v.Item3 / (v.Item2+v.Item3) + v.Item3 / 2).Sum() / 7;
            Console.WriteLine($"Old GAS reward per block per NEO: { ((decimal)rpbpn_old) / ((decimal)GovernanceRetriever.Program.Instance.agentstates.Select(v=>v.Item2).Sum()) } ");
            var rpbpn_new = selected.Select(v => (v, GovernanceRetriever.Program.Instance.filtered.Value.Where(w => w.Item1.SequenceEqual(v)).Single().Item2, selected.Zip(votes).Where(w => w.Item1.SequenceEqual(v)).Single().Item2, elected.Take(14).Select(v => (v.Item1, BigInteger.Parse("1"))).Concat(elected.TakeLast(7).Select(v => (v.Item1, BigInteger.Parse("2"))) ).Where(w => w.Item1.SequenceEqual(v)).Single().Item2 )).Select(v => BigInteger.Parse("100000000") * v.Item4*v.Item3 / (v.Item2+v.Item3) + v.Item3 / 2).Sum() / 7;
            Console.WriteLine($"New GAS reward per block per NEO: { ((decimal)rpbpn_new) / ((decimal)GovernanceRetriever.Program.Instance.agentstates.Select(v=>v.Item2).Sum()) } ");
        }
    }
}
