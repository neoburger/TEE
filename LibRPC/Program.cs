using System;
using LibHelper;
using Neo;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.Network.RPC.Models;
using Neo.VM;
using Neo.VM.Types;

namespace LibRPC
{
    public static class Program
    {
        private static readonly string RPC = Environment.GetEnvironmentVariable("RPC");
        private static readonly Uri URI = new(RPC);
        private static readonly ProtocolSettings settings = ProtocolSettings.Load("/dev/stdin");
        private static readonly RpcClient CLI = new(URI, null, null, settings);
        static void Main(string[] args)
        {
            Call(new byte[] { ((byte)OpCode.RET) });
            "OK!".Log();
        }
        public static StackItem[] Call(byte[] script)
        {
            RpcInvokeResult result = CLI.InvokeScriptAsync(script).GetAwaiter().GetResult();
            if (result.State != VMState.HALT)
            {
                throw new Exception();
            }
            return result.Stack;
        }
        public static TransactionManagerFactory factory = new(CLI);
        public static UInt256 Send(Transaction tx) => CLI.SendRawTransactionAsync(tx).GetAwaiter().GetResult();
    }
}
