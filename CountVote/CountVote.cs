namespace Neo.Plugins;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Cryptography;
using Neo.Cryptography.ECC;
using Neo.SmartContract;
using Neo.VM;
using System.Numerics;
using System.Text;
using System.IO;
using Newtonsoft.Json;
using System.Collections.Generic;

public class CountVote : Plugin, IPersistencePlugin
{
    private static readonly string REPOSITORY_LOCAL_PATH = Environment.GetEnvironmentVariable("REPOSITORY_LOCAL_PATH") ?? throw new Exception("No REPOSITORY_LOCAL_PATH in environment variable");
    // long NBIP_ID instead of ulong, because C# parses integers in json as long
    private static readonly long NBIP_ID = long.TryParse(Environment.GetEnvironmentVariable("NBIP_ID"), out NBIP_ID) ? NBIP_ID : throw new Exception("No NBIP_ID in environment variable");
    private static readonly ulong COUNT_VOTE_SINCE_BLOCK = ulong.TryParse(Environment.GetEnvironmentVariable("COUNT_VOTE_SINCE_BLOCK"), out COUNT_VOTE_SINCE_BLOCK) ? COUNT_VOTE_SINCE_BLOCK : 0;
    private static readonly ulong COUNT_VOTE_UNTIL_TIME = ulong.TryParse(Environment.GetEnvironmentVariable("COUNT_VOTE_UNTIL_TIME"), out COUNT_VOTE_UNTIL_TIME) ? COUNT_VOTE_UNTIL_TIME : 99999999999990;
    private static readonly uint COUNT_VOTE_EVERY_BLOCKS = uint.TryParse(Environment.GetEnvironmentVariable("COUNT_VOTE_EVERY_BLOCKS"), out COUNT_VOTE_EVERY_BLOCKS) ? COUNT_VOTE_EVERY_BLOCKS : 21;
    private static readonly UInt160 TEE = UInt160.Parse("0x82450b644631506b6b7194c4071d0b98d762771f");
    private static readonly UInt160 DAO = UInt160.Parse("0x54806765d451e2b0425072730d527d05fbfa9817");
    void IPersistencePlugin.OnPersist(NeoSystem system, Block block, DataCache snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
    {
        if (block.Index % COUNT_VOTE_EVERY_BLOCKS != 0) { return; }
        if (COUNT_VOTE_SINCE_BLOCK > block.Index)
        {
            throw new Exception($"COUNT_VOTE_SINCE_BLOCK {COUNT_VOTE_SINCE_BLOCK} > block.Index {block.Index}");
        }
        if (block.Timestamp > COUNT_VOTE_UNTIL_TIME)
        {
            throw new Exception($"current block time {block.Timestamp} > COUNT_VOTE_UNTIL_TIME {COUNT_VOTE_UNTIL_TIME}");
        }
        BigInteger votes = AnalyzeVoteFilesOfBranch(system, snapshot, path: REPOSITORY_LOCAL_PATH);
        BigInteger totalSupply = ApplicationEngine.Run(DAO.MakeScript("totalSupply", new object[] { }), snapshot, settings: system.Settings).ResultStack.Select(v => v.GetInteger()).First();
        Console.WriteLine($"Block{block.Index}:NBIP-{NBIP_ID}:{votes}/{totalSupply}");
        //throw new Exception("abort");
    }

    BigInteger AnalyzeVoteFilesOfBranch(NeoSystem system, DataCache snapshot, string path = "")
    {
        
        if (path == "") { path = REPOSITORY_LOCAL_PATH; }
        UInt160 voter;
        bool forOrAgainst;
        BigInteger totalVote = 0;
        foreach (string fileFullPath in Directory.GetFiles(path, "0x*.json"))
        {
            string filename = System.IO.Path.GetFileName(fileFullPath);
            if (filename.StartsWith("0x") && filename.EndsWith(".json") && filename.Length == 47)
            {
                Tuple<UInt160, bool> result = VerifyVoteFile(filename, path: System.IO.Path.GetDirectoryName(fileFullPath)??"");
                voter = result.Item1; forOrAgainst = result.Item2;
                if (voter == UInt160.Zero) { continue; }
                ApplicationEngine balanceOfVoterRun = ApplicationEngine.Run(DAO.MakeScript("balanceOf", new object[] { voter }), snapshot, settings: system.Settings);
                BigInteger balanceOfVoter = balanceOfVoterRun.ResultStack.Select(v => v.GetInteger()).First();
                if (forOrAgainst) { totalVote += balanceOfVoter; }
                else { totalVote -= balanceOfVoter; }
            }
        }
        return totalVote;
    }

    Tuple<UInt160, bool> VerifyVoteFile(string filename, string path = "")
    {
        if (path == "") { path = REPOSITORY_LOCAL_PATH; }
        string text = System.IO.File.ReadAllText(System.IO.Path.Combine(path, filename));
        try
        {
            Dictionary<string, object> json = JsonConvert.DeserializeObject<Dictionary<string, object>>(text);
            UInt160 voter = UInt160.Parse(filename[..42]);
            if ((long)json["NBIP"] != NBIP_ID) throw new Exception("Wrong NBIP ID in json");
            switch (json["PROFILE"])
            {
                case "NEOLINE":
                    string signature = (string)json["SIGNATURE"];
                    string extra = (string)json["EXTRA"];
                    if (extra.Length < 32) { throw new Exception("Too short EXTRA"); }
                    ECPoint publicKey = ECPoint.Parse(extra[32..], ECCurve.Secp256r1);
                    if (Contract.CreateSignatureRedeemScript(publicKey).ToScriptHash() != voter) { throw new Exception("Public key does not match scripthash"); }
                    bool forOrAgainst = (bool)json["YES"];
                    string forOrAganistString = forOrAgainst ? "FOR" : "AGAINST";
                    string message = $"I VOTE {forOrAganistString} NBIP-{NBIP_ID}.";
                    if (message.Length > 64) { throw new Exception("Invalid message"); }
                    List<byte> signedMessage = (new byte[] { 0x01, 0x00, 0x01, 0xf0, (byte)(message.Length+32) }).ToList();
                    signedMessage = signedMessage.Concat(Encoding.ASCII.GetBytes(extra[..32])).ToList();
                    signedMessage = signedMessage.Concat(Encoding.ASCII.GetBytes(message).ToList()).ToList();
                    signedMessage = signedMessage.Concat(new byte[] { 0x00, 0x00 }).ToList();
                    if (!Crypto.VerifySignature(signedMessage.ToArray(), Convert.FromHexString(signature), publicKey)) { throw new Exception("Invalid signature"); }
                    return new Tuple<UInt160, bool>(voter, forOrAgainst);
                default:
                    throw new Exception("Invalid profile. Only NEOLINE is supported for now.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(path, filename);
            Console.WriteLine(text);
            Console.WriteLine(ex);
            return new Tuple<UInt160, bool>(UInt160.Zero, false);
        }
    }
}