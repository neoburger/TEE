using System;
using Neo;
using System.Linq;
using System.Numerics;

namespace StrategyOperator
{
    class Program
    {
        public static Program Instance = new();
        static void Main(string[] args)
        {
            var src = GovernanceRetriever.Program.Instance.agentstates.Select((value, i) => (i, value.Item1, value.Item2)).ToList();
            var dst = StrategySolver.Program.Instance.selected.Zip(StrategySolver.Program.Instance.votes).ToList();

            var keepTargetSrc = src.Where(v => dst.Where(w => w.Item1.SequenceEqual(v.Item2)).Any()).ToList();
            var changeTargetSrc = src.Where(v => !dst.Where(w => w.Item1.SequenceEqual(v.Item2)).Any()).Zip(dst.Where(v => !src.Where(w => w.Item2.SequenceEqual(v.Item1)).Any())).Select(v => (v.First.Item1, v.Second.Item1, v.First.Item3));
            src.Where(v => !dst.Where(w => w.Item1.SequenceEqual(v.Item2)).Any()).Zip(dst.Where(v => !src.Where(w => w.Item2.SequenceEqual(v.Item1)).Any())).ToList().ForEach(v => Console.WriteLine($"set vote target for agent{v.First.Item1} from {v.First.Item2.ToHexString()} to {v.Second.Item1.ToHexString()}"));
            var newSrc = keepTargetSrc.Concat(changeTargetSrc).ToList();

            var difference = newSrc.Select(v => (v.Item1, v.Item2, v.Item3 - dst.Where(w => w.Item1.SequenceEqual(v.Item2)).First().Item2)).ToList();
            difference.Sort((x, y) => -x.Item3.CompareTo(y.Item3));

            BigInteger prefixSum = 0;
            var sumDifference = difference.Select(v => (v.Item1, v.Item2, prefixSum += v.Item3)).ToList();
            sumDifference.SkipLast(1).Select((value, i) => (value, i)).ToList().ForEach(v => Console.WriteLine($"transfer from agent{v.Item1.Item1} to agent{difference[v.Item2 + 1].Item1} for {v.Item1.Item3}"));
        }
    }
}
