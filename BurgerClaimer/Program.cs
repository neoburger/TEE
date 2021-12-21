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
        private static readonly BigInteger Threashold = BigInteger.Parse(Environment.GetEnvironmentVariable("THREASHOLD"));

        static void Main(string[] args)
        {
            UInt160 BNEO = UInt160.Parse("0x48c40d4666f93408be1bef038b6722404d9a4c2a");
            uint blocknum = LibRPC.Program.CLI.GetBlockCountAsync().GetAwaiter().GetResult();
            List<UInt160> AGENTS = Enumerable.Range(0, 21).Select(v => BNEO.MakeScript("agent", v)).SelectMany(a => a).ToArray().Call().TakeWhile(v => v.IsNull == false).Select(v => v.ToU160()).ToList();
            $"AGENTS: {String.Join(", ", AGENTS)}".Log();

            var unclaimedGas = AGENTS.Select(v => NativeContract.NEO.Hash.MakeScript("unclaimedGas", v, blocknum).Call()[0].GetInteger());
            var claimableGas = AGENTS.Select(v => NativeContract.GAS.Hash.MakeScript("balanceOf", v).Call()[0].GetInteger()).Zip(unclaimedGas).Select(v => v.Item1 + v.Item2);
            
            $"unclaimedGas: {String.Join(", ", unclaimedGas.Select(v => v.ToString()))}".Log();
            $"claimableGas: {String.Join(", ", claimableGas.Select(v => v.ToString()))}".Log();

            var syncScripts = AGENTS.Select(v => v.MakeScript("sync"));
            var claimScripts = AGENTS.Select(v => v.MakeScript("claim"));
            
            var scripts = syncScripts.Zip(unclaimedGas).Where(v => v.Second > Threashold).Select(v => v.First).Concat(
                claimScripts.Zip(claimableGas).Where(v => v.Second > Threashold).Select(v => v.First)
            );

            scripts.Aggregate((v, w) => v.ToList().Concat(w.ToList()).ToArray())?.SendTx();
        }
    }
}
