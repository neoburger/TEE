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
using Neo.SmartContract;

namespace FTWFLM
{
    class Program
    {
        private static readonly string? IGNOREFEE = Environment.GetEnvironmentVariable("IGNOREFEE");
        static UInt160 NEPSwap = UInt160.Parse("0x997ced5777a3f66485d66828bda3864b8c8bdf95");
        static UInt160 FLMRouter = UInt160.Parse("0xf970f4ccecd765b63732b821775dc38c25d74f23");
        static UInt160 FLMSwap = UInt160.Parse("0x4d5a85b0c83777df72cfb665a933970e4e20c0ec");
        static UInt160 FLM = UInt160.Parse("0xf0151f528127558851b39c2cd8aa47da7418ab28");
        static UInt160 bNEO = UInt160.Parse("0x48c40d4666f93408be1bef038b6722404d9a4c2a");
        static UInt160 NEP = UInt160.Parse("0xf853a98ac55a756ae42379a312d55ddfdf7c8514");
        static Neo.VM.Types.ByteString estimated = new(System.Text.Encoding.UTF8.GetBytes("estimated"));
        static Neo.VM.Types.ByteString amountA = new(System.Text.Encoding.UTF8.GetBytes("amountA"));
        static Neo.VM.Types.ByteString amountB = new(System.Text.Encoding.UTF8.GetBytes("amountB"));

        static BigInteger FEE = 1000_0000;
        static BigInteger expire = TimeProvider.Current.UtcNow.ToTimestampMS() + 3600_000;
        static void Main(string[] args)
        {
            List<BigInteger> bNEOFLMres = FLMSwap.MakeScript("getReserves").Call().Single().ToVMStruct().Select(v => v.GetInteger()).ToList(); // bNEO, FLM
            List<BigInteger> FLMNEPres = NEPSwap.MakeScript("getReserve", new object[] { FLM, NEP }).Call().Single().ToVMMap().Where(v => v.Key.Equals(amountA) || v.Key.Equals(amountB)).Select(v => v.Value.GetInteger()).ToList(); // FLM, NEP
            List<BigInteger> bNEONEPres = NEPSwap.MakeScript("getReserve", new object[] { bNEO, NEP }).Call().Single().ToVMMap().Where(v => v.Key.Equals(amountA) || v.Key.Equals(amountB)).Select(v => v.Value.GetInteger()).ToList(); // bNEO, NEP

            BigInteger biggest = BigInteger.Zero;
            BigInteger IN = 0;
            BigInteger OUT = 0;
            BigInteger PROFIT = 0;
            for (IN = 1_000000; IN < 500_00000000; IN += 1_000000)
            {
                OUT = SwapBNEOtoFLM(IN, bNEOFLMres, FLMNEPres, bNEONEPres);
                PROFIT = OUT - IN;
                if (PROFIT < biggest) { break; } else { biggest = PROFIT; }
            }
            $"BENO->NEP->FLM->BNEO: IN:{(double)IN / 1_0000_0000},OUT:{(double)OUT / 1_0000_0000},PROFIT:{(double)PROFIT / 1_0000_0000}".Log();

            if (PROFIT > FEE)
            {
                throw new Exception($"BENO->NEP->FLM->BNEO: IN:{(double)IN / 1_0000_0000},OUT:{(double)OUT / 1_0000_0000},PROFIT:{(double)PROFIT / 1_0000_0000}");
            }


            for (IN = 1_000000; IN < 500_00000000; IN += 1_000000)
            {
                OUT = SwapFLMtoBENO(IN, bNEOFLMres, FLMNEPres, bNEONEPres);
                PROFIT = OUT - IN;
                if (PROFIT < biggest) { break; } else { biggest = PROFIT; }
            }
            $"BENO->FLM->NEP->BNEO: IN:{(double)IN / 1_0000_0000},OUT:{(double)OUT / 1_0000_0000},PROFIT:{(double)PROFIT / 1_0000_0000}".Log();

            if (PROFIT > FEE)
            {
                throw new Exception($"BENO->FLM->NEP->BNEO: IN:{(double)IN / 1_0000_0000},OUT:{(double)OUT / 1_0000_0000},PROFIT:{(double)PROFIT / 1_0000_0000}");
            }
        }
        public static BigInteger SwapBNEOtoFLM(BigInteger bNEOIN, List<BigInteger> bNEOFLMres, List<BigInteger> FLMNEPres, List<BigInteger> bNEONEPres)
        {
            BigInteger NEPOUT = FTWGetAmountOut(bNEOIN, bNEONEPres[0], bNEONEPres[1]);
            BigInteger FLMOUT = FTWGetAmountOut(NEPOUT, FLMNEPres[1], FLMNEPres[0]);
            BigInteger bNEOOUT = FLMGetAmountOut(FLMOUT, bNEOFLMres[1], bNEOFLMres[0]);
            return bNEOOUT;
        }


        public static BigInteger SwapFLMtoBENO(BigInteger bNEOIN, List<BigInteger> bNEOFLMres, List<BigInteger> FLMNEPres, List<BigInteger> bNEONEPres)
        {
            BigInteger FLMOUT = FLMGetAmountOut(bNEOIN, bNEOFLMres[0], bNEOFLMres[1]);
            BigInteger NEPOUT = FTWGetAmountOut(FLMOUT, FLMNEPres[0], FLMNEPres[1]);
            BigInteger bNEOOUT = FTWGetAmountOut(NEPOUT, bNEONEPres[1], bNEONEPres[0]);
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
