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

namespace GovernanceRetriever
{
    class Program
    {
        private static readonly string RPC = Environment.GetEnvironmentVariable("RPC");
        private static readonly Uri URI = new(RPC);
        private static readonly ProtocolSettings settings = ProtocolSettings.Load("/dev/stdin");
        private static readonly RpcClient CLI = new(URI, null, null, settings);
        private static readonly UInt160 BNEO = UInt160.Parse("0x48c40d4666f93408be1bef038b6722404d9a4c2a");
        public List<UInt160> agents;
        public List<(byte[], BigInteger)> candidates;
        public List<(byte[], BigInteger)> agentstates;
        public Lazy<List<(byte[], BigInteger)>> filtered;
        public Lazy<BigInteger> neo;
        static void Main(string[] args)
        {
            Program program = new();
            Console.WriteLine($"AGENT LIST:");
            program.agents.ForEach(v => Console.WriteLine($"{v}"));
            Console.WriteLine($"");
            Console.WriteLine($"AGENT STATUS(VOTE_TARGET: NEOBALANCE):");
            program.agentstates.ForEach(v => Console.WriteLine($"{v.Item1.ToHexString()}: {v.Item2}"));
            Console.WriteLine($"");
            Console.WriteLine($"CANDIDATE STATUS(PUBLICKEY: NEOVOTED):");
            program.agentstates.ForEach(v => Console.WriteLine($"{v.Item1.ToHexString()}: {v.Item2}"));
            Console.WriteLine($"");
            Console.WriteLine($"CANDIDATE STATUS BURGER REMOVED(PUBLICKEY: NEOVOTED):");
            program.filtered.Value.ForEach(v => Console.WriteLine($"{v.Item1.ToHexString()}: {v.Item2}"));
            Console.WriteLine($"");
            Console.WriteLine($"NEO HOLD: {program.neo.Value}");
        }
        Program()
        {
            agents = GetAgents();
            candidates = GetCandidates();
            agentstates = GetAccountStates(agents);
            filtered = new(() => candidates.Select(v => (v.Item1, v.Item2 - agentstates.Where(w => w.Item1.SequenceEqual(v.Item1)).SingleOrDefault().Item2)).ToList());
            neo = new(() => agentstates.Select(v => v.Item2).Sum());
        }
        static StackItem[] InvokeScript(byte[] script)
        {
            RpcInvokeResult result = CLI.InvokeScriptAsync(script).GetAwaiter().GetResult();
            if (result.State != VMState.HALT)
            {
                throw new Exception();
            }
            return result.Stack;
        }
        static List<UInt160> GetAgents()
        {
            byte[] script = Enumerable.Range(0, 21).Select(v => BNEO.MakeScript("agent", v)).SelectMany(a => a).ToArray();
            return InvokeScript(script).TakeWhile(v => v.IsNull == false).Select(v => new UInt160(v.GetSpan())).ToList();
        }
        static List<(byte[], BigInteger)> GetAccountStates(IEnumerable<UInt160> agents)
        {
            byte[] script = agents.Select(v => NativeContract.NEO.Hash.MakeScript("getAccountState", v)).SelectMany(a => a).ToArray();
            return InvokeScript(script).Select(v => (Neo.VM.Types.Struct)v).Select(v => (v.Last().GetSpan().ToArray(), v.First().GetInteger())).ToList();
        }
        static List<(byte[], BigInteger)> GetCandidates()
        {
            byte[] script = NativeContract.NEO.Hash.MakeScript("getCandidates");
            return InvokeScript(script).Select(v => (Neo.VM.Types.Array)v).Single().Select(v => (Neo.VM.Types.Struct)v).Select(v => (v.First().GetSpan().ToArray(), v.Last().GetInteger())).ToList();
        }
    }
}
