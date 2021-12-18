using System;
using LibHelper;
using LibRPC;
using Neo;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.Wallets;

namespace LibWallet
{
    public static class Program
    {
        private static readonly string WIF = Environment.GetEnvironmentVariable("WIF");
        private static KeyPair keypair = Neo.Network.RPC.Utility.GetKeyPair(WIF);
        private static UInt160 contract = Contract.CreateSignatureContract(keypair.PublicKey).ScriptHash;
        private static Signer[] signers = new[] { new Signer { Scopes = WitnessScope.CalledByEntry, Account = contract } };
        static void Main(string[] args)
        {
            contract.Out();
            string SCRIPT = Environment.GetEnvironmentVariable("SCRIPT");
            if (SCRIPT is null) return;
            SCRIPT.HexToBytes().SendTx().Out();
        }
        public static UInt256 SendTx(this byte[] script) => script.TxMgr().AddSignature(keypair).SignAsync().GetAwaiter().GetResult().Send();
    }
}
