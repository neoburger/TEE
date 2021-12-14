using System;
using Neo;
using Neo.Network.RPC;
using Neo.VM;
using Neo.Network.RPC.Models;
using Neo.VM.Types;

namespace NeoRpcClient
{
    public class Program
    {
        private static readonly string RPC = Environment.GetEnvironmentVariable("RPC");
        private static readonly Uri URI = new(RPC);
        private static readonly ProtocolSettings settings = ProtocolSettings.Load("/dev/stdin");
        private static readonly RpcClient CLI = new(URI, null, null, settings);
        public static Program Instance = new();
        static void Main(string[] args)
        {
            Instance.InvokeScript(new byte[] { ((byte)OpCode.RET) });
            Console.WriteLine("OK");
        }
        private Program()
        {
        }
        public StackItem[] InvokeScript(byte[] script)
        {
            RpcInvokeResult result = CLI.InvokeScriptAsync(script).GetAwaiter().GetResult();
            if (result.State != VMState.HALT)
            {
                throw new Exception();
            }
            return result.Stack;
        }
        public RpcClient Get()
        {
            return CLI;
        }
    }
}
