using System;
using System.Numerics;
using System.Linq;
using Neo;
using Neo.SmartContract.Native;
using Neo.VM;
using System.Collections.Generic;
using LibHelper;
using LibRPC;
using LibWallet;

namespace BurgerClaimer
{
    class Program
    {
        private static readonly BigInteger THREASHOLD = BigInteger.Parse(Environment.GetEnvironmentVariable("THREASHOLD"));

        static void Main(string[] args)
        {
            UInt160 BNEO = UInt160.Parse("0x48c40d4666f93408be1bef038b6722404d9a4c2a");
            BigInteger BLOCKNUM = NativeContract.Ledger.Hash.MakeScript("currentIndex").Call().Single().GetInteger();
            List<UInt160> AGENTS = Enumerable.Range(0, 22).Select(v => BNEO.MakeScript("agent", v)).SelectMany(a => a).ToArray().Call().Where(v => v.IsNull == false).Select(v => v.ToU160()).ToList();
            $"BLOCKNUM: {BLOCKNUM}".Log();
            $"AGENTS: {String.Join(", ", AGENTS)}".Log();

            List<BigInteger> UNCLAIMED = AGENTS.Select(v => NativeContract.NEO.Hash.MakeScript("unclaimedGas", v, BLOCKNUM).Call().Single().GetInteger()).ToList();
            List<BigInteger> GASBALANCE = AGENTS.Select(v => NativeContract.GAS.Hash.MakeScript("balanceOf", v).Call().Single().GetInteger()).ToList();
            List<BigInteger> MERGED = UNCLAIMED.Zip(GASBALANCE).Select(v => v.First > 10 ? v.First + v.Second : v.Second).ToList();
            $"UNCLAIMED: {String.Join(", ", UNCLAIMED)}".Log();
            $"GASBALANCE: {String.Join(", ", GASBALANCE)}".Log();
            $"MERGED: {String.Join(", ", MERGED)}".Log();

            List<byte[]> SYNC = AGENTS.Zip(UNCLAIMED).Where(v => v.Second > THREASHOLD).Select(v => v.First.MakeScript("sync")).ToList();
            List<byte[]> CLAIM = AGENTS.Zip(MERGED).Where(v => v.Second > THREASHOLD).Select(v => v.First.MakeScript("claim")).ToList();
            $"SYNC: {String.Join(", ", SYNC.Select(v => v.ToHexString()))}".Log();
            $"CLAIM: {String.Join(", ", CLAIM.Select(v => v.ToHexString()))}".Log();
            SYNC.Concat(CLAIM).SelectMany(v => v).Nullize()?.ToArray().SendTx().Out();
        }
    }
}
