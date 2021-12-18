using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LibHelper;
using LibWallet;
using LibRPC;
using Neo;
using Neo.SmartContract.Native;
using Neo.VM;

namespace Strategist
{
    class Program
    {
        static void Main(string[] args)
        {
            UInt160 BNEO = UInt160.Parse("0x48c40d4666f93408be1bef038b6722404d9a4c2a");
            $"BNEO: {BNEO}".Log();

            List<UInt160> AGENTS = Enumerable.Range(0, 21).Select(v => BNEO.MakeScript("agent", v)).SelectMany(a => a).ToArray().Call().TakeWhile(v => v.IsNull == false).Select(v => v.ToU160()).ToList();
            $"AGENTS: {String.Join(", ", AGENTS)}".Log();

            List<Neo.VM.Types.Struct> candidates = NativeContract.NEO.Hash.MakeScript("getCandidates").Call().Single().ToVMArray().Select(v => v.ToVMStruct()).ToList();
            List<byte[]> CANDIDATES = candidates.Select(v => v.First().ToBytes()).ToList();
            List<BigInteger> CANDIDATE_VOTES = candidates.Select(v => v.Last().GetInteger()).ToList();
            $"CANDIDATES: {String.Join(", ", CANDIDATES.Select(v => v.ToHexString()))}".Log();
            $"CANDIDATE_VOTES: {String.Join(", ", CANDIDATE_VOTES)}".Log();

            List<Neo.VM.Types.Struct> agentstates = AGENTS.Select(v => NativeContract.NEO.Hash.MakeScript("getAccountState", v)).SelectMany(a => a).ToArray().Call().Select(v => v.ToVMStruct()).ToList();
            List<byte[]> AGENT_TO = agentstates.Select(v => v.Last().ToBytes()).ToList();
            List<BigInteger> AGENT_HOLD = agentstates.Select(v => v.First().GetInteger()).ToList();
            $"AGENT_TO: {String.Join(", ", AGENT_TO.Select(v => v.ToHexString()))}".Log();
            $"AGENT_HOLD: {String.Join(", ", AGENT_HOLD)}".Log();

            List<BigInteger> CANDIDATE_V = CANDIDATES.Zip(CANDIDATE_VOTES).Select(v => v.Second - AGENT_TO.Zip(AGENT_HOLD).FindByOrDefault(v.First)).ToList();
            $"CANDIDATE_V: {String.Join(", ", CANDIDATE_V)}".Log();

            List<(byte[], BigInteger)> filtered = CANDIDATES.Zip(CANDIDATE_V).ToList();
            filtered.SortBy(v => v.Item2);

            List<byte[]> ELECTEDS = filtered.TakeLast(21).Select(v => v.Item1).ToList();
            $"ELECTEDS: {String.Join(", ", ELECTEDS.Select(v => v.ToHexString()))}".Log();

            List<byte[]> CNS = ELECTEDS.TakeLast(7).ToList();
            List<byte[]> CMS = ELECTEDS.Take(14).ToList();
            List<BigInteger> ELECTED_K = ELECTEDS.Select(v => CNS.HasBytes(v) ? 200000000 * BigInteger.One : 100000000 * BigInteger.One).ToList();
            List<BigInteger> CM_V = CMS.Select(v => CANDIDATES.Zip(CANDIDATE_V).FindBy(v)).ToList();
            $"CNS: {String.Join(", ", CNS.Select(v => v.ToHexString()))}".Log();
            $"CMS: {String.Join(", ", CMS.Select(v => v.ToHexString()))}".Log();
            $"CM_V: {String.Join(", ", CM_V)}".Log();

            // TODO: FIX
            (AGENTS.Count < 12).Assert();

            List<byte[]> SELECTS = CMS.Take(AGENTS.Count).ToList();
            List<BigInteger> SELECT_K = SELECTS.Select(v => ELECTEDS.Zip(ELECTED_K).FindByOrDefault(v)).ToList();
            List<BigInteger> SELECT_V = SELECTS.Select(v => CANDIDATES.Zip(CANDIDATE_V).FindBy(v)).ToList();
            $"SELECTS: {String.Join(", ", SELECTS.Select(v => v.ToHexString()))}".Log();
            $"SELECT_K: {String.Join(", ", SELECT_K)}".Log();
            $"SELECT_V: {String.Join(", ", SELECT_V)}".Log();

            BigInteger N = AGENT_HOLD.Sum() + SELECT_V.Sum();
            $"N: {N}".Log();

            List<double> SELECT_T = SELECT_K.Zip(SELECT_V).Select(v => Math.Sqrt((double)(v.First * v.Second))).ToList();
            $"SELECT_T: {String.Join(", ", SELECT_T)}".Log();


            double U = (double)N / SELECT_T.Sum();
            $"U: {U}".Log();

            List<BigInteger> SELECT_VOTES = SELECT_T.Select(v => v * U).Select(v => (BigInteger)v).ToList();
            $"SELECT_VOTES: {String.Join(", ", SELECT_VOTES)}".Log();


            List<BigInteger> SELECT_HOLD = SELECT_VOTES.Zip(SELECT_V).Select(v => v.First - v.Second).ToList();
            SELECT_HOLD[0] += AGENT_HOLD.Sum() - SELECT_HOLD.Sum();
            $"SELECT_HOLD: {String.Join(", ", SELECT_HOLD)}".Log();
            SELECT_HOLD.ForEach(v => { if (v < 0) throw new Exception(); });

            BigInteger SCORE0 = AGENT_TO.Zip(AGENT_HOLD).Select(v => ELECTEDS.Zip(ELECTED_K).FindByOrDefault(v.First) * v.Second / (v.Second + CANDIDATES.Zip(CANDIDATE_V).FindBy(v.First))).Sum();
            BigInteger SCORE = SELECTS.Zip(SELECT_HOLD).Select(v => ELECTEDS.Zip(ELECTED_K).FindByOrDefault(v.First) * v.Second / (v.Second + CANDIDATES.Zip(CANDIDATE_V).FindBy(v.First))).Sum();
            $"SCORE: {SCORE0} => {SCORE}".Log();
            (SCORE0 <= SCORE).Assert();
            // TODO: FIX
            if (SCORE / (SCORE - SCORE0) > 10000)
            {
                $"NOT GOOD ENOUGH".Log();
                return;
            }

            Queue<byte[]> changes = new(SELECTS.Where(v => AGENT_TO.HasBytes(v) == false));
            List<byte[]> AGENT_TON = AGENT_TO.Select(v => SELECTS.HasBytes(v) ? v : changes.Dequeue()).ToList();

            List<byte[]> SCRIPTVOTES = AGENT_TON.Zip(AGENT_TO).Where(v => v.First.SequenceEqual(v.Second) == false).Select(v => BNEO.MakeScript("trigVote", AGENT_TON.IndexOf(v.First), v.First)).ToList();
            $"SCRIPTVOTES: {String.Join(", ", SCRIPTVOTES.Select(v => v.ToHexString()))}".Log();

            List<BigInteger> AGENT_HOLDN = AGENT_TON.Select(v => SELECTS.Zip(SELECT_HOLD).FindBy(v)).ToList();
            $"AGENT_HOLDN: {String.Join(", ", AGENT_HOLDN)}".Log();

            List<BigInteger> AGENT_DIFF = AGENT_HOLDN.Zip(AGENT_HOLD).Select(v => v.First - v.Second).ToList();
            $"AGENT_DIFF: {String.Join(", ", AGENT_DIFF)}".Log();

            List<(BigInteger, int)> diff = AGENT_DIFF.Select((v, i) => (v, i)).Where(v => v.Item1.IsZero == false).ToList();
            diff.Sort();
            List<int> TRANSFERS = diff.Select(v => v.Item2).ToList();
            List<BigInteger> TRANSFER_AMOUNT = diff.Select(v => v.Item1).ToList();
            $"TRANSFERS: {String.Join(", ", TRANSFERS)}".Log();
            $"TRANSFER_AMOUNT: {String.Join(", ", TRANSFER_AMOUNT)}".Log();

            List<BigInteger> actions = TRANSFER_AMOUNT.Aggregate((BigInteger.Zero, Enumerable.Empty<BigInteger>()), (stack, v) => (stack.Item1 - v, stack.Item2.Append(stack.Item1 - v))).Item2.ToList();
            if (actions.Last() != 0) throw new Exception();

            List<BigInteger> ACTIONS = actions.SkipLast(1).ToList();
            $"ACTIONS: {String.Join(", ", ACTIONS)}".Log();

            List<byte[]> SCRIPTTRANSFERS = ACTIONS.Select((v, i) => BNEO.MakeScript("trigTransfer", TRANSFERS[i], TRANSFERS[i + 1], v)).ToList();
            $"SCRIPTTRANSFERS: {String.Join(", ", SCRIPTTRANSFERS.Select(v => v.ToHexString()))}".Log();

            SCRIPTVOTES.Concat(SCRIPTTRANSFERS).SelectMany(a => a).ToArray().SendTx().Out();
        }
    }
}
