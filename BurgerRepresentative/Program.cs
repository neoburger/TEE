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

namespace BurgerRepresentative
{
    class Program
    {
        private static readonly BigInteger THREASHOLD = BigInteger.Parse(Environment.GetEnvironmentVariable("THREASHOLD"));

        static void Main(string[] args)
        {
            UInt160 BNEO = UInt160.Parse("0x48c40d4666f93408be1bef038b6722404d9a4c2a");
            UInt160 REPRESENTATIVE = UInt160.Parse("0x329aeff39c13550337f02296d5ffc82583acaba3");

            BigInteger BLOCKNUM = NativeContract.Ledger.Hash.MakeScript("currentIndex").Call().Single().GetInteger();
            BigInteger GASBALANCE = NativeContract.GAS.Hash.MakeScript("balanceOf", new object[]{ REPRESENTATIVE }).Call().Single().GetInteger();

            $"GASBALANCE: {GASBALANCE}".Log();

            if (GASBALANCE < THREASHOLD || GASBALANCE < 1) {
                $"GASBALANCE < THREASHOLD: {GASBALANCE} < {THREASHOLD}".Log();
                return;
            }

            byte[] REWARD = NativeContract.GAS.Hash.MakeScript("transfer", new object[]{ REPRESENTATIVE, BNEO, GASBALANCE-1, "reward"});

            REWARD.SendTx().Out();
        }
    }
}
