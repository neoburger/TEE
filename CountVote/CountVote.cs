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
using Newtonsoft.Json;
using Octokit;
using System.Collections.Generic;

public class CountVote : Plugin, IPersistencePlugin
{
    private static readonly Credentials GITHUB_TOKEN = new(Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? throw new Exception("No GITHUB_TOKEN in environment variable"));
    private GitHubClient client = new(new ProductHeaderValue("commitsClient"));
    private static readonly string GITHUB_OWNER = Environment.GetEnvironmentVariable("GITHUB_OWNER") ?? "vang1ong7ang";
    private static readonly string GITHUB_REPOSITORY = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY") ?? "nbip";
    private static readonly string GITHUB_BRANCH = Environment.GetEnvironmentVariable("GITHUB_BRANCH") ?? throw new Exception("No GITHUB_BRANCH in environment variable");
    private static readonly ulong COUNT_VOTE_SINCE_BLOCK = ulong.TryParse(Environment.GetEnvironmentVariable("COUNT_VOTE_SINCE_BLOCK"), out COUNT_VOTE_UNTIL_TIME) ? COUNT_VOTE_UNTIL_TIME : 0;
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
        ulong currentTime = TimeProvider.Current.UtcNow.ToTimestampMS();
        if (currentTime > COUNT_VOTE_UNTIL_TIME)
        {
            throw new Exception($"current time {currentTime} > COUNT_VOTE_UNTIL_TIME {COUNT_VOTE_UNTIL_TIME}");
        }
        client.Credentials = GITHUB_TOKEN;
        BigInteger votes = AnalyzeVoteFilesOfBranch(system, snapshot);
        BigInteger totalSupply = ApplicationEngine.Run(DAO.MakeScript("totalSupply", new object[] {}), snapshot, settings: system.Settings).ResultStack.Select(v => v.GetInteger()).First();
        Console.WriteLine($"Block{block.Index}:{GITHUB_BRANCH}:{votes}/{totalSupply}");
        //throw new Exception("abort");
    }

    BigInteger AnalyzeVoteFilesOfBranch(NeoSystem system, DataCache snapshot, string branch="")
    {
        if(branch == "") { branch = GITHUB_BRANCH; }
        UInt160 voter;
        bool forOrAgainst;
        BigInteger totalVote = 0;
        foreach (RepositoryContent file in GetBranchTree(branch))
        {
            if(file.Name.StartsWith("0x") && file.Name.EndsWith(".json") && file.Name.Length == 47)
            {
                Tuple<UInt160, bool> result = VerifyVoteFile(file.Path, GITHUB_BRANCH);
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

    Tuple<UInt160, bool> VerifyVoteFile(string path, string branch="")
    {
        if (branch == "") { branch = GITHUB_BRANCH; }
        long nbipID = long.Parse(branch[5..]);
        var contentClient = client.Repository.Content;
        var task = contentClient.GetRawContentByRef(GITHUB_OWNER, GITHUB_REPOSITORY, path, GITHUB_BRANCH);
        task.Wait();
        byte[] content = task.Result;
        try
        {
            Dictionary<string, object> json = JsonConvert.DeserializeObject<Dictionary<string, object>>(Encoding.UTF8.GetString(content));
            UInt160 voter = UInt160.Parse(path[..42]);
            if ((long)json["NBIP"] != nbipID) throw new Exception("Wrong NBIP ID in json");
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
                    string message = $"I VOTE {forOrAganistString} {branch}.";
                    if(message.Length > 64) { throw new Exception("Invalid message"); }
                    List<byte> signedMessage = (new byte[] { 0x01, 0x00, 0x01, 0xf0, (byte)(message.Length+32) }).ToList();
                    signedMessage = signedMessage.Concat(Encoding.ASCII.GetBytes(extra[..32])).ToList();
                    signedMessage = signedMessage.Concat(Encoding.ASCII.GetBytes(message).ToList()).ToList();
                    signedMessage = signedMessage.Concat(new byte[] { 0x00, 0x00 }).ToList();
                    if(!Crypto.VerifySignature(signedMessage.ToArray(), Convert.FromHexString(signature), publicKey)) { throw new Exception("Invalid signature"); }
                    return new Tuple<UInt160, bool>(voter, forOrAgainst);
                default:
                    throw new Exception("Invalid profile. Only NEOLINE is supported for now.");
            }
        }catch (Exception ex)
        {
            Console.WriteLine(task.Result);
            Console.WriteLine(ex);
            return new Tuple<UInt160, bool>(UInt160.Zero, false);
        }
    }

    IReadOnlyList<RepositoryContent> GetBranchTree(string branch="")
    {
        if(branch == "") { branch = GITHUB_BRANCH; }
        var contentClient = client.Repository.Content;
        var task = contentClient.GetAllContentsByRef(owner: GITHUB_OWNER, name: GITHUB_REPOSITORY, reference: branch);
        task.Wait();
        return task.Result;
    }

    static IReadOnlyList<GitHubCommit> GetBranchCommits()
    {
        var client = new GitHubClient(new ProductHeaderValue("commitsClient"))
        {
            Credentials = GITHUB_TOKEN
        };
        var commitClient = client.Repository.Commit;
        var commitRequest = new CommitRequest { Sha=GITHUB_BRANCH };
        var apiOptions = new ApiOptions { PageCount=int.MaxValue, StartPage=1, PageSize=100 };
        var task = commitClient.GetAll(owner: GITHUB_OWNER, name: GITHUB_REPOSITORY, request: commitRequest, options: apiOptions);
        task.Wait();
        return task.Result;
    }
}
