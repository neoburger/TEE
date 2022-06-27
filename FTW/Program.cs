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

namespace BurgerClaimer
{
    class Program
    {
        private static readonly string? IGNOREFEE = Environment.GetEnvironmentVariable("IGNOREFEE");
        static UInt160 NEPSwap = UInt160.Parse("0x997ced5777a3f66485d66828bda3864b8c8bdf95");
        static UInt160 FLMRouter = UInt160.Parse("0xf970f4ccecd765b63732b821775dc38c25d74f23");
        static UInt160 FLMSwap = UInt160.Parse("0x3244fcadcccff190c329f7b3083e4da2af60fbce");
        static UInt160 GAS = UInt160.Parse("0xd2a4cff31913016155e38e474a2c06d08be276cf");
        static UInt160 bNEO = UInt160.Parse("0x48c40d4666f93408be1bef038b6722404d9a4c2a");
        static UInt160 NEP = UInt160.Parse("0xf853a98ac55a756ae42379a312d55ddfdf7c8514");
        static Neo.VM.Types.ByteString estimated = new(System.Text.Encoding.UTF8.GetBytes("estimated"));
        static Neo.VM.Types.ByteString amountA = new(System.Text.Encoding.UTF8.GetBytes("amountA"));
        static Neo.VM.Types.ByteString amountB = new(System.Text.Encoding.UTF8.GetBytes("amountB"));

        static BigInteger FEE = 1000_0000;
        static BigInteger expire = TimeProvider.Current.UtcNow.ToTimestampMS() + 3600_000;
        static UInt160 me = LibWallet.Program.NEOGASsigners.Single().Account;
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

            if (PROFIT > FEE)
            {
                throw new Exception($"BENO->NEP->GAS->BNEO: IN:{(double)IN / 1_0000_0000},OUT:{(double)OUT / 1_0000_0000},PROFIT:{(double)PROFIT / 1_0000_0000}");
            }


            for (IN = 1_000000; IN < 500_00000000; IN += 1_000000)
            {
                OUT = SwapGAStoBENO(IN, bNEOGASres, GASNEPres, bNEONEPres);
                PROFIT = OUT - IN;
                if (PROFIT < biggest) { break; } else { biggest = PROFIT; }
            }
            $"BENO->GAS->NEP->BNEO: IN:{(double)IN / 1_0000_0000},OUT:{(double)OUT / 1_0000_0000},PROFIT:{(double)PROFIT / 1_0000_0000}".Log();

            if (PROFIT > FEE)
            {
                throw new Exception($"BENO->GAS->NEP->BNEO: IN:{(double)IN / 1_0000_0000},OUT:{(double)OUT / 1_0000_0000},PROFIT:{(double)PROFIT / 1_0000_0000}");
            }
        }
        public static BigInteger SwapBNEOtoGAS(BigInteger bNEOIN, List<BigInteger> bNEOGASres, List<BigInteger> GASNEPres, List<BigInteger> bNEONEPres)
        {
            BigInteger NEPOUT = FTWGetAmountOut(bNEOIN, bNEONEPres[0], bNEONEPres[1]);
            BigInteger GASOUT = FTWGetAmountOut(NEPOUT, GASNEPres[1], GASNEPres[0]);
            BigInteger bNEOOUT = FLMGetAmountOut(GASOUT, bNEOGASres[1], bNEOGASres[0]);
            return bNEOOUT;
        }

        public static void DoSwapBNEOtoGAS(BigInteger bNEOIN, List<BigInteger> bNEOGASres, List<BigInteger> GASNEPres, List<BigInteger> bNEONEPres)
        {
            BigInteger NEPOUT = FTWGetAmountOut(bNEOIN, bNEONEPres[0], bNEONEPres[1]);
            var swap1 = NEPSwap.MakeScript("swap", new object[]{
                me,
                bNEO,
                bNEOIN,
                NEP,
                NEPOUT,
                expire,
            });
            //TODO delete
            NEPOUT = 805489797;
            BigInteger GASOUT = FTWGetAmountOut(NEPOUT, GASNEPres[1], GASNEPres[0]);
            var swap2 = NEPSwap.MakeScript("swap", new object[]{
                me,
                NEP,
                NEPOUT,
                GAS,
                GASOUT,
                expire,
            });
            if (IGNOREFEE is null) {
                GASOUT -= FEE;
            }
            BigInteger bNEOOUT = FLMGetAmountOut(GASOUT, bNEOGASres[1], bNEOGASres[0]);
            var builder = new ScriptBuilder();
            builder.EmitPush(expire);
            builder.CreateArray(new object[]{GAS, bNEO});
            builder.EmitPush(bNEOOUT * 994 / 1000);
            builder.EmitPush(GASOUT);
            builder.EmitPush(me);
            builder.EmitPush(5);
            builder.Emit(OpCode.PACK);
            builder.EmitPush(CallFlags.All);
            builder.EmitPush("swapTokenInForTokenOut");
            builder.EmitPush(FLMRouter);
            builder.EmitSysCall(ApplicationEngine.System_Contract_Call);
            var swap3 = builder.ToArray();

            swap1.ToArray().SendTx(LibWallet.Program.NEOGASsigners).Out();
            int i = 0;
            for (i = 0; i < 60; i++) {
                if (LibRPC.Program.CLI.InvokeScriptAsync(swap2.Concat(swap3).ToArray(), LibWallet.Program.NEOGASsigners).GetAwaiter().GetResult().State != VMState.HALT) {
                    System.Threading.Thread.Sleep(1000);
                } else {
                    swap2.Concat(swap3).ToArray().SendTx(LibWallet.Program.NEOGASsigners).Out();
                    break;
                }
            }
            if (i == 60) {
                throw new Exception("failed to convert next steps");
            }
        }


        public static BigInteger SwapGAStoBENO(BigInteger bNEOIN, List<BigInteger> bNEOGASres, List<BigInteger> GASNEPres, List<BigInteger> bNEONEPres)
        {
            BigInteger GASOUT = FLMGetAmountOut(bNEOIN, bNEOGASres[0], bNEOGASres[1]);
            BigInteger NEPOUT = FTWGetAmountOut(GASOUT, GASNEPres[0], GASNEPres[1]);
            BigInteger bNEOOUT = FTWGetAmountOut(NEPOUT, bNEONEPres[1], bNEONEPres[0]);
            return bNEOOUT;
        }

        public static void DoSwapGAStoBENO(BigInteger bNEOIN, List<BigInteger> bNEOGASres, List<BigInteger> GASNEPres, List<BigInteger> bNEONEPres)
        {
            BigInteger GASOUT = FLMGetAmountOut(bNEOIN, bNEOGASres[0], bNEOGASres[1]);

            var builder = new ScriptBuilder();
            builder.EmitPush(expire);
            builder.CreateArray(new object[]{bNEO, GAS});
            builder.EmitPush(GASOUT);
            builder.EmitPush(bNEOIN);
            builder.EmitPush(me);
            builder.EmitPush(5);
            builder.Emit(OpCode.PACK);
            builder.EmitPush(CallFlags.All);
            builder.EmitPush("swapTokenInForTokenOut");
            builder.EmitPush(FLMRouter);
            builder.EmitSysCall(ApplicationEngine.System_Contract_Call);
            var swap1 = builder.ToArray();
            // var swap1 = FLMRouter.MakeScript("swapTokenInForTokenOut", new object[]{
            //     me,
            //     bNEOIN,
            //     GASOUT,
            //     new UInt160[]{
            //         bNEO,
            //         GAS
            //     },
            //     expire,
            // });
            if (IGNOREFEE is null) {
                GASOUT -= FEE;
            }
            BigInteger NEPOUT = FTWGetAmountOut(GASOUT, GASNEPres[0], GASNEPres[1]);
            var swap2 = NEPSwap.MakeScript("swap", new object[]{
                me,
                GAS,
                GASOUT,
                NEP,
                NEPOUT,
                expire,
            });
            BigInteger bNEOOUT = FTWGetAmountOut(NEPOUT, bNEONEPres[1], bNEONEPres[0]);
            var swap3 = NEPSwap.MakeScript("swap", new object[]{
                me,
                NEP,
                NEPOUT,
                bNEO,
                bNEOOUT,
                expire,
            });
            swap1.Concat(swap2).Concat(swap3).ToArray().ToHexString().Log();
            // swap1.Concat(swap2).Concat(swap3).ToArray().SendTx(LibWallet.Program.NEOGASsigners).Out();
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
