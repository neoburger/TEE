using System;
using Neo.SmartContract;
using Neo.Wallets;

namespace AddressPrinter
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine($"SCRIPTHASH = {Contract.CreateSignatureContract(new KeyPair(Wallet.GetPrivateKeyFromWIF(Environment.GetEnvironmentVariable("WIF"))).PublicKey).ScriptHash}");
        }
    }
}
