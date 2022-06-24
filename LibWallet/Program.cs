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
        public static Signer[] NEOGASsigners = new[] { new Signer { Scopes = WitnessScope.CustomContracts, Account = contract, AllowedContracts = new UInt160[] { UInt160.Parse("0xf970f4ccecd765b63732b821775dc38c25d74f23"), UInt160.Parse("0xfb75a5314069b56e136713d38477f647a13991b4"), UInt160.Parse("0xca2d20610d7982ebe0bed124ee7e9b2d580a6efc"), UInt160.Parse("0xd2a4cff31913016155e38e474a2c06d08be276cf"), UInt160.Parse("0x3244fcadcccff190c329f7b3083e4da2af60fbce"), UInt160.Parse("0x48c40d4666f93408be1bef038b6722404d9a4c2a"), UInt160.Parse("0x997ced5777a3f66485d66828bda3864b8c8bdf95"), UInt160.Parse("0xf853a98ac55a756ae42379a312d55ddfdf7c8514"), } } };
        static void Main(string[] args)
        {
            contract.Out();
            string SCRIPT = Environment.GetEnvironmentVariable("SCRIPT");
            if (SCRIPT is null) return;
            SCRIPT.HexToBytes().SendTx().Out();
        }
        public static UInt256 SendTx(this byte[] script, Signer[] signer = null) => script.TxMgr(signer.Length==0?signers:signer).AddSignature(keypair).SignAsync().GetAwaiter().GetResult().Send();
    }
}
