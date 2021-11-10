using System;
using System.Security.Cryptography;
using Neo.Wallets;

namespace KeyGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            var sk = new byte[32];
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create()) { rng.GetBytes(sk); }
            Console.WriteLine(new KeyPair(sk).Export());
        }
    }
}
