using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LibHelper;
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
            $"".Log();

            List<UInt160> AGENTS = Enumerable.Range(0, 21).Select(v => BNEO.MakeScript("agent", v)).SelectMany(a => a).ToArray().Call().TakeWhile(v => v.IsNull == false).Select(v => v.ToU160()).ToList();

            $"AGENTS: ".Log();
            AGENTS.ForEach(v => v.Log());
            $"".Log();

            List<Neo.VM.Types.Struct> candidates = NativeContract.NEO.Hash.MakeScript("getCandidates").Call().Single().ToVMArray().Select(v => v.ToVMStruct()).ToList();
            List<byte[]> CANDIDATES = candidates.Select(v => v.First().ToBytes()).ToList();
            List<BigInteger> CANDIDATE_VOTES = candidates.Select(v => v.Last().GetInteger()).ToList();

            $"CANDIDATES:".Log();
            CANDIDATES.ForEach(v => v.ToHexString().Log());
            $"".Log();

            $"CANDIDATE_VOTES:".Log();
            CANDIDATE_VOTES.ForEach(v => v.Log());
            $"".Log();

            List<Neo.VM.Types.Struct> agentstates = AGENTS.Select(v => NativeContract.NEO.Hash.MakeScript("getAccountState", v)).SelectMany(a => a).ToArray().Call().Select(v => v.ToVMStruct()).ToList();
            List<byte[]> AGENT_VOTE_TARGETS = agentstates.Select(v => v.Last().ToBytes()).ToList();
            List<BigInteger> AGENT_VOTE_AMOUNTS = agentstates.Select(v => v.First().GetInteger()).ToList();

            $"AGENT_VOTE_TARGETS:".Log();
            AGENT_VOTE_TARGETS.ForEach(v => v.ToHexString().Log());
            $"".Log();

            $"AGENT_VOTE_AMOUNTS:".Log();
            AGENT_VOTE_AMOUNTS.ForEach(v => v.Log());
            $"".Log();

            List<BigInteger> CANDIDATE_VOTES_FILTERED = CANDIDATES.Zip(CANDIDATE_VOTES).Select(v => v.Second - (AGENT_VOTE_TARGETS.HasBytes(v.First) ? AGENT_VOTE_TARGETS.Zip(AGENT_VOTE_AMOUNTS).FindBy(v.First) : 0)).ToList();

            $"CANDIDATE_VOTES_FILTERED:".Log();
            CANDIDATE_VOTES_FILTERED.ForEach(v => v.Log());
            $"".Log();

            BigInteger NEO_HELD = AGENT_VOTE_AMOUNTS.Sum();

            $"NEO_HELD:".Log();
            NEO_HELD.Log();
            $"".Log();

            List<(byte[], BigInteger)> filtered = CANDIDATES.Zip(CANDIDATE_VOTES_FILTERED).ToList();
            filtered.SortBy(v => v.Item2);

            List<byte[]> CANDIDATES_ELECTED = filtered.TakeLast(21).Select(v => v.Item1).ToList();

            $"CANDIDATES_ELECTED:".Log();
            CANDIDATES_ELECTED.ForEach(v => v.ToHexString().Log());
            $"".Log();

            List<byte[]> CANDIDATES_CN = CANDIDATES_ELECTED.TakeLast(7).ToList();
            List<byte[]> CANDIDATES_CM = CANDIDATES_ELECTED.Take(14).ToList();
            List<BigInteger> CANDIDATES_CM_VOTES = CANDIDATES_CM.Select(v => CANDIDATES.Zip(CANDIDATE_VOTES_FILTERED).FindBy(v)).ToList();

            $"CANDIDATES_CM:".Log();
            CANDIDATES_CM.ForEach(v => v.ToHexString().Log());
            $"".Log();

            $"CANDIDATES_CM_VOTES:".Log();
            CANDIDATES_CM_VOTES.ForEach(v => v.Log());
            $"".Log();

            // TODO: FIX
            if (AGENTS.Count > 12)
            {
                throw new Exception();
            }

            List<byte[]> CANDIDATES_SELECTED = CANDIDATES_CM.Take(AGENTS.Count).ToList();
            List<BigInteger> CANDIDATES_SELECTED_VOTES = CANDIDATES_CM_VOTES.Take(AGENTS.Count).ToList();

            $"CANDIDATES_SELECTED:".Log();
            CANDIDATES_SELECTED.ForEach(v => v.ToHexString().Log());
            $"".Log();

            $"CANDIDATES_SELECTED_VOTES:".Log();
            CANDIDATES_SELECTED_VOTES.ForEach(v => v.Log());
            $"".Log();

            List<double> K = Enumerable.Repeat(1.0, AGENTS.Count).ToList();
            List<double> V = CANDIDATES_SELECTED_VOTES.Select(v => ((double)v)).ToList();

            $"K:".Log();
            K.ForEach(v => v.Log());
            $"".Log();

            $"V:".Log();
            V.ForEach(v => v.Log());
            $"".Log();
            // TODO: END

            double N = ((double)NEO_HELD) + V.Sum();

            $"N:".Log();
            N.Log();
            $"".Log();

            List<double> T = K.Zip(V).Select(v => Math.Sqrt(v.First * v.Second)).ToList();

            $"T:".Log();
            T.ForEach(v => v.Log());
            $"".Log();

            double U = N / T.Sum();

            $"U:".Log();
            U.Log();
            $"".Log();

            List<BigInteger> CANDIDATES_SELECTED_VOTES_TARGET = T.Select(v => v * U).Select(v => (BigInteger)v).ToList();

            $"CANDIDATES_SELECTED_VOTES_TARGET:".Log();
            CANDIDATES_SELECTED_VOTES_TARGET.ForEach(v => v.Log());
            $"".Log();

            List<BigInteger> CANDIDATES_SELECTED_VOTES_AMOUNT = CANDIDATES_SELECTED_VOTES_TARGET.Zip(CANDIDATES_SELECTED_VOTES).Select(v => v.First - v.Second).ToList();
            CANDIDATES_SELECTED_VOTES_AMOUNT[0] += NEO_HELD - CANDIDATES_SELECTED_VOTES_AMOUNT.Sum();

            $"CANDIDATES_SELECTED_VOTES_AMOUNT:".Log();
            CANDIDATES_SELECTED_VOTES_AMOUNT.ForEach(v => v.Log());
            $"".Log();

            CANDIDATES_SELECTED_VOTES_AMOUNT.ForEach(v => { if (v < 0) throw new Exception(); });

            BigInteger SCORE0 = AGENT_VOTE_TARGETS.Zip(AGENT_VOTE_AMOUNTS).Select(v => (CANDIDATES_CM.HasBytes(v.First) ? 100000000 : CANDIDATES_CN.HasBytes(v.First) ? 200000000 : 0) * v.Second / (v.Second + CANDIDATES.Zip(CANDIDATE_VOTES_FILTERED).FindBy(v.First))).Sum();
            BigInteger SCORE = CANDIDATES_SELECTED.Zip(CANDIDATES_SELECTED_VOTES_AMOUNT).Select(v => (CANDIDATES_CM.HasBytes(v.First) ? 100000000 : CANDIDATES_CN.HasBytes(v.First) ? 200000000 : 0) * v.Second / (v.Second + CANDIDATES.Zip(CANDIDATE_VOTES_FILTERED).FindBy(v.First))).Sum();

            $"SCORE0:".Log();
            SCORE0.Log();
            $"".Log();

            $"SCORE:".Log();
            SCORE.Log();
            $"".Log();
        }
    }
}
