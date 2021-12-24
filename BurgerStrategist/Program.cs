using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LibHelper;
using LibRPC;
using LibWallet;
using Neo;
using Neo.SmartContract.Native;
using Neo.VM;

namespace BurgerStrategist
{
    class Program
    {
        static void Main(string[] args)
        {
            UInt160 BNEO = UInt160.Parse("0x48c40d4666f93408be1bef038b6722404d9a4c2a");

            List<UInt160> AGENTS = Enumerable.Range(0, 21).Select(v => BNEO.MakeScript("agent", v)).SelectMany(a => a).ToArray().Call().TakeWhile(v => v.IsNull == false).Select(v => v.ToU160()).ToList();
            (List<byte[]> CANDIDATES, List<BigInteger> CANDIDATE_VOTES) = NativeContract.NEO.Hash.MakeScript("getCandidates").Call().Single().ToVMArray().Select(v => v.ToVMStruct()).Map2(v => v.First().ToBytes(), v => v.Last().GetInteger());
            (List<byte[]> AGENT_TO, List<BigInteger> AGENT_HOLD) = AGENTS.Select(v => NativeContract.NEO.Hash.MakeScript("getAccountState", v)).SelectMany(a => a).ToArray().Call().Select(v => v.ToVMStruct()).Map2(v => v.Last().ToBytes(), v => v.First().GetInteger());

            $"AGENTS: {String.Join(", ", AGENTS)}".Log();
            $"CANDIDATES: {String.Join(", ", CANDIDATES.Select(v => v.ToHexString()))}".Log();
            $"CANDIDATE_VOTES: {String.Join(", ", CANDIDATE_VOTES)}".Log();
            $"AGENT_TO: {String.Join(", ", AGENT_TO.Select(v => v.ToHexString()))}".Log();
            $"AGENT_HOLD: {String.Join(", ", AGENT_HOLD)}".Log();

            List<BigInteger> CANDIDATE_V = CANDIDATES.Zip(CANDIDATE_VOTES).Select(v => v.Second - AGENT_TO.Zip(AGENT_HOLD).FindByOrDefault(v.First)).ToList();
            List<byte[]> ELECTEDS = CANDIDATES.Zip(CANDIDATE_V).OrderBy(v => v.Second).TakeLast(21).Select(v => v.Item1).ToList();
            List<byte[]> CNS = ELECTEDS.TakeLast(7).ToList();
            List<byte[]> CMS = ELECTEDS.Take(14).ToList();
            $"CANDIDATE_V: {String.Join(", ", CANDIDATE_V)}".Log();
            $"ELECTEDS: {String.Join(", ", ELECTEDS.Select(v => v.ToHexString()))}".Log();
            $"CNS: {String.Join(", ", CNS.Select(v => v.ToHexString()))}".Log();
            $"CMS: {String.Join(", ", CMS.Select(v => v.ToHexString()))}".Log();

            List<BigInteger> ELECTED_K = ELECTEDS.Select(v => CNS.HasBytes(v) ? 200000000 * BigInteger.One : 100000000 * BigInteger.One).ToList();
            List<BigInteger> CM_V = CMS.Select(v => CANDIDATES.Zip(CANDIDATE_V).FindBy(v)).ToList();
            $"CM_V: {String.Join(", ", CM_V)}".Log();

            // TODO: FIX
            (AGENTS.Count < 12).Assert();

            List<byte[]> SELECTS = CMS.Take(AGENTS.Count).ToList();
            List<BigInteger> SELECT_K = SELECTS.Select(v => ELECTEDS.Zip(ELECTED_K).FindByOrDefault(v)).ToList();
            List<BigInteger> SELECT_V = SELECTS.Select(v => CANDIDATES.Zip(CANDIDATE_V).FindBy(v)).ToList();
            $"SELECTS: {String.Join(", ", SELECTS.Select(v => v.ToHexString()))}".Log();
            $"SELECT_K: {String.Join(", ", SELECT_K)}".Log();
            $"SELECT_V: {String.Join(", ", SELECT_V)}".Log();

            List<BigInteger> SELECT_HOLD = Solve(SELECT_K, SELECT_V, AGENT_HOLD.Sum());
            $"FINAL SELECT_HOLD: {String.Join(", ", SELECT_HOLD)}".Log();

            BigInteger SCORE0 = AGENT_TO.Zip(AGENT_HOLD).Select(v => ELECTEDS.Zip(ELECTED_K).FindByOrDefault(v.First) * v.Second / (v.Second + CANDIDATES.Zip(CANDIDATE_V).FindBy(v.First))).Sum();
            BigInteger SCORE = SELECTS.Zip(SELECT_HOLD).Select(v => ELECTEDS.Zip(ELECTED_K).FindByOrDefault(v.First) * v.Second / (v.Second + CANDIDATES.Zip(CANDIDATE_V).FindBy(v.First))).Sum();
            $"SCORE: {SCORE0} => {SCORE}".Log();
            (SCORE0 <= SCORE).Assert();
            if (SCORE / (SCORE + 1 - SCORE0) > 1024)
            {
                $"NOT GOOD ENOUGH".Log();
                return;
            }

            List<byte[]> AGENT_TON = AGENT_TO.Merge(SELECTS.Where(v => !AGENT_TO.HasBytes(v)), v => !SELECTS.HasBytes(v)).ToList();
            $"AGENT_TON: {String.Join(", ", AGENT_TON.Select(v => v.ToHexString()))}".Log();

            List<byte[]> SCRIPTVOTES = AGENT_TON.Zip(AGENT_TO).Where(v => v.First.SequenceEqual(v.Second) == false).Select(v => BNEO.MakeScript("trigVote", AGENT_TON.IndexOf(v.First), v.First)).ToList();
            $"SCRIPTVOTES: {String.Join(", ", SCRIPTVOTES.Select(v => v.ToHexString()))}".Log();

            List<BigInteger> AGENT_HOLDN = AGENT_TON.Select(v => SELECTS.Zip(SELECT_HOLD).FindBy(v)).ToList();
            $"AGENT_HOLDN: {String.Join(", ", AGENT_HOLDN)}".Log();

            List<BigInteger> AGENT_DIFF = AGENT_HOLDN.Zip(AGENT_HOLD).Select(v => v.First - v.Second).ToList();
            $"AGENT_DIFF: {String.Join(", ", AGENT_DIFF)}".Log();

            (List<int> TRANSFERS, List<BigInteger> TRANSFER_AMOUNT) = AGENT_DIFF.Select((v, i) => (v, i)).Where(v => v.v.IsZero == false).OrderBy(v => v.v).Map2(v => v.i, v => v.v);
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

        static List<BigInteger> Solve(List<BigInteger> K, List<BigInteger> V, BigInteger N)
        {
            List<double> T = K.Zip(V).Select(v => Math.Sqrt((double)(v.First * v.Second))).ToList();
            $"T: {String.Join(", ", T)}".Log();

            double U = (double)(N + V.Sum()) / T.Sum();
            $"U: {U}".Log();

            List<BigInteger> VOTES = T.Select(v => v * U).Select(v => (BigInteger)v).ToList();
            $"VOTES: {String.Join(", ", VOTES)}".Log();

            List<BigInteger> HOLD = VOTES.Zip(V).Select(v => v.First - v.Second).ToList();
            HOLD[0] += N - HOLD.Sum();
            $"HOLD: {String.Join(", ", HOLD)}".Log();

            List<BigInteger> FLAG = HOLD.Select(v => v < 1 ? BigInteger.One : BigInteger.Zero).ToList();
            return FLAG.Sum() == 0 ? HOLD : FLAG.Merge(Solve(K.Zip(HOLD).Where(v => v.Second > 0).Select(v => v.First).ToList(), V.Zip(HOLD).Where(v => v.Second > 0).Select(v => v.First).ToList(), N - FLAG.Sum()), v => v == BigInteger.Zero).ToList();
        }
    }
}
