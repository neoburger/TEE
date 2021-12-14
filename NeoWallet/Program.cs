using System;
using Neo;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.SmartContract;
using Neo.Wallets;

namespace NeoWallet
{
    public class Program
    {
        private static readonly string WIF = Environment.GetEnvironmentVariable("WIF");
        private static KeyPair keypair = Neo.Network.RPC.Utility.GetKeyPair(WIF);
        private static UInt160 contract = Contract.CreateSignatureContract(keypair.PublicKey).ScriptHash;
        private static Signer[] signers = new[] { new Signer { Scopes = WitnessScope.CalledByEntry, Account = contract } };
        private static RpcClient client = NeoRpcClient.Program.Instance.Get();
        private static TransactionManagerFactory factory = new TransactionManagerFactory(client);
        public static Program Instance = new();
        static void Main(string[] args)
        {
            string SCRIPT = Environment.GetEnvironmentVariable("SCRIPT");
            byte[] script = SCRIPT.HexToBytes();
            UInt256 txid = Instance.SendTx(script);
            Console.WriteLine(txid);
        }
        private Program()
        {
        }
        public UInt256 SendTx(byte[] script)
        {
            TransactionManager manager = factory.MakeTransactionAsync(script, signers).GetAwaiter().GetResult();
            Transaction tx = manager.AddSignature(keypair).SignAsync().GetAwaiter().GetResult();
            return client.SendRawTransactionAsync(tx).GetAwaiter().GetResult();
        }
    }
}
