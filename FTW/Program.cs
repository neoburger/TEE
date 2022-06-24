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
        static UInt160 NEPSwap = UInt160.Parse("0x997ced5777a3f66485d66828bda3864b8c8bdf95");
        static UInt160 FLMSwap = UInt160.Parse("0x3244fcadcccff190c329f7b3083e4da2af60fbce");
        static UInt160 GAS = UInt160.Parse("0xd2a4cff31913016155e38e474a2c06d08be276cf");
        static UInt160 bNEO = UInt160.Parse("0x48c40d4666f93408be1bef038b6722404d9a4c2a");
        static UInt160 NEP = UInt160.Parse("0xf853a98ac55a756ae42379a312d55ddfdf7c8514");
        static Neo.VM.Types.ByteString estimated = new(System.Text.Encoding.UTF8.GetBytes("estimated"));
        static Neo.VM.Types.ByteString amountA = new(System.Text.Encoding.UTF8.GetBytes("amountA"));
        static Neo.VM.Types.ByteString amountB = new(System.Text.Encoding.UTF8.GetBytes("amountB"));

        static BigInteger FEE = 8000_0000;
        static void Main(string[] args)
        {
            List<BigInteger> bNEOGASres = FLMSwap.MakeScript("getReserves").Call().Single().ToVMStruct().Select(v => v.GetInteger()).ToList(); // bNEO, GAS
            List<BigInteger> GASNEPres = NEPSwap.MakeScript("getReserve", new object[] { GAS, NEP }).Call().Single().ToVMMap().Where(v => v.Key.Equals(amountA) || v.Key.Equals(amountB)).Select(v => v.Value.GetInteger()).ToList(); // GAS, NEP
            List<BigInteger> bNEONEPres = NEPSwap.MakeScript("getReserve", new object[] { bNEO, NEP }).Call().Single().ToVMMap().Where(v => v.Key.Equals(amountA) || v.Key.Equals(amountB)).Select(v => v.Value.GetInteger()).ToList(); // bNEO, NEP

            BigInteger biggest = BigInteger.Zero;
            BigInteger IN = 0;
            BigInteger OUT = 0;
            BigInteger PROFIT = 0;
            for (IN = 1_000000; IN < 500_00000000; IN += 1_000000)
            {
                OUT = SwapBNEOtoGAS(IN, bNEOGASres, GASNEPres, bNEONEPres);
                PROFIT = OUT - IN;
                if (PROFIT < biggest) { break; } else { biggest = PROFIT; }
            }
            $"BENO->NEP->GAS->BNEO: IN:{(double)IN / 1_0000_0000},OUT:{(double)OUT / 1_0000_0000},PROFIT:{(double)PROFIT / 1_0000_0000}".Log();

            if (PROFIT > FEE) { throw new Exception($"BENO->NEP->GAS->BNEO: IN:{(double)IN / 1_0000_0000},OUT:{(double)OUT / 1_0000_0000},PROFIT:{(double)PROFIT / 1_0000_0000}"); }


            for (IN = 1_000000; IN < 500_00000000; IN += 1_000000)
            {
                OUT = SwapGAStoBENO(IN, bNEOGASres, GASNEPres, bNEONEPres);
                PROFIT = OUT - IN;
                if (PROFIT < biggest) { break; } else { biggest = PROFIT; }
            }
            $"BENO->GAS->NEP->BNEO: IN:{(double)IN / 1_0000_0000},OUT:{(double)OUT / 1_0000_0000},PROFIT:{(double)PROFIT / 1_0000_0000}".Log();

            if (PROFIT > FEE) { throw new Exception($"BENO->GAS->NEP->BNEO: IN:{(double)IN / 1_0000_0000},OUT:{(double)OUT / 1_0000_0000},PROFIT:{(double)PROFIT / 1_0000_0000}"); }
        }
        public static BigInteger SwapGAStoBENO(BigInteger bNEOIN, List<BigInteger> bNEOGASres, List<BigInteger> GASNEPres, List<BigInteger> bNEONEPres)
        {
            BigInteger GASOUT = FLMGetAmountOut(bNEOIN, bNEOGASres[0], bNEOGASres[1]);
            BigInteger NEPOUT = FTWGetAmountOut(GASOUT, GASNEPres[0], GASNEPres[1]);
            BigInteger bNEOOUT = FTWGetAmountOut(NEPOUT, bNEONEPres[1], bNEONEPres[0]);
            return bNEOOUT;
        }
        public static BigInteger SwapBNEOtoGAS(BigInteger bNEOIN, List<BigInteger> bNEOGASres, List<BigInteger> GASNEPres, List<BigInteger> bNEONEPres)
        {
            BigInteger NEPOUT = FTWGetAmountOut(bNEOIN, bNEONEPres[0], bNEONEPres[1]);
            BigInteger GASOUT = FTWGetAmountOut(NEPOUT, GASNEPres[1], GASNEPres[0]);
            BigInteger bNEOOUT = FLMGetAmountOut(GASOUT, bNEOGASres[1], bNEOGASres[0]);
            return bNEOOUT;
        }
        public static BigInteger FLMGetAmountOut(BigInteger amountIn, BigInteger reserveIn, BigInteger reserveOut)
        {
            var amountInWithFee = amountIn * 997;
            var numerator = amountInWithFee * reserveOut;
            var denominator = reserveIn * 1000 + amountInWithFee;
            return numerator / denominator;
        }
        public static BigInteger FTWGetAmountOut(BigInteger amountIn, BigInteger reserveIn, BigInteger reserveOut)
        {
            BigInteger invariant = reserveIn * reserveOut;
            BigInteger fee = amountIn / 400;
            BigInteger rIN = reserveIn + amountIn - fee;
            BigInteger rOUT = (invariant / rIN) + 1;
            return reserveOut - rOUT;
        }
    }
}
