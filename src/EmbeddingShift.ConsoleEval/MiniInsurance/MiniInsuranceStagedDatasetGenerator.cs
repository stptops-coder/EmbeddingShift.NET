namespace EmbeddingShift.ConsoleEval.MiniInsurance;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

internal static class MiniInsuranceStagedDatasetGenerator
{
    private sealed record Topic(string Key, string Title, string[] Keywords);

    private sealed record QueryDef(string Id, string Text, string RelevantDocId);

    private sealed record DatasetManifest(
        string DatasetName,
        string CreatedUtc,
        int Seed,
        int Stages,
        int BasePolicies,
        int BaseQueries,
        int ConfusersStage1,
        string Notes);

    private sealed record StageManifest(
        int StageIndex,
        string StageName,
        int Policies,
        int Queries,
        bool CorpusDriftD1,
        bool QueryDriftD2);

    private static readonly Topic[] Topics = new[]
    {
        new Topic("fire", "Fire & Smoke", new[] { "fire", "smoke", "flame", "soot", "extinguishing", "scorch" }),
        new Topic("water", "Water Damage", new[] { "water", "leak", "burst pipe", "flood", "moisture", "mould" }),
        new Topic("theft", "Theft & Burglary", new[] { "theft", "burglary", "break-in", "stolen", "robbery", "forced entry" }),
        new Topic("liability", "Liability", new[] { "liability", "third-party", "negligence", "bodily injury", "property damage" }),
        new Topic("glass", "Glass Breakage", new[] { "glass", "window", "pane", "shattered", "replacement", "glazing" }),
        new Topic("legal", "Legal Protection", new[] { "legal", "attorney", "lawsuit", "defence costs", "court", "fees" }),
        new Topic("cyber", "Cyber Incident", new[] { "cyber", "phishing", "breach", "ransomware", "data", "incident response" }),
        new Topic("business", "Business Interruption", new[] { "interruption", "downtime", "revenue", "extra expenses", "waiting period" }),
    };

    public static string Generate(
        string datasetName,
        int stages,
        int basePolicies,
        int baseQueries,
        int seed,
        bool overwrite,
        Action<string> log)
    {
        if (stages <= 0) throw new ArgumentOutOfRangeException(nameof(stages));
        if (basePolicies <= 0) throw new ArgumentOutOfRangeException(nameof(basePolicies));
        if (baseQueries <= 0) throw new ArgumentOutOfRangeException(nameof(baseQueries));
        if (string.IsNullOrWhiteSpace(datasetName))
            throw new ArgumentException("Dataset name must not be empty.", nameof(datasetName));

        var name = datasetName.Trim();
        var datasetRoot = MiniInsurancePaths.GetDatasetRoot(name);

        if (Directory.Exists(datasetRoot) && Directory.EnumerateFileSystemEntries(datasetRoot).Any())
        {
            if (!overwrite)
                throw new InvalidOperationException(
                    $"Dataset '{name}' already exists: {datasetRoot}. Use --overwrite to replace it.");

            Directory.Delete(datasetRoot, recursive: true);
            Directory.CreateDirectory(datasetRoot);
        }

        var rng = new Random(seed);

        // Stage plan:
        //  stage-00: baseline
        //  stage-01: D1 corpus drift (add confusers, queries unchanged)
        //  stage-02: D2 query drift (policies keep stage-01, queries drifted)
        var confusersStage1 = Math.Max(1, basePolicies / 4);

        var stage0Policies = BuildBasePolicies(basePolicies);
        var stage0Queries = BuildBaseQueries(stage0Policies, baseQueries, rng);

        for (var s = 0; s < stages; s++)
        {
            var stageName = $"stage-{s:00}";
            var stageRoot = MiniInsurancePaths.GetStageRoot(name, s);

            WriteStage(
                stageRoot,
                stageIndex: s,
                stageName: stageName,
                basePolicies: stage0Policies,
                baseQueries: stage0Queries,
                rng: rng,
                addCorpusConfusers: s >= 1,
                confuserCount: confusersStage1,
                driftQueries: s >= 2);

            log($"[Generator] Wrote {stageName} -> {stageRoot}");
        }

        var manifest = new DatasetManifest(
            DatasetName: name,
            CreatedUtc: DateTime.UtcNow.ToString("O"),
            Seed: seed,
            Stages: stages,
            BasePolicies: stage0Policies.Count,
            BaseQueries: stage0Queries.Count,
            ConfusersStage1: confusersStage1,
            Notes: "stage-00 baseline; stage-01 adds D1 corpus confusers; stage-02 applies D2 query drift. Use EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT to point the workflow to a specific stage folder."
        );

        WriteJson(Path.Combine(datasetRoot, "dataset.manifest.json"), manifest);

        var suggestedStage0 = MiniInsurancePaths.GetStageRoot(name, 0);

        log("");
        log("Next (PowerShell):");
        log($"  $env:EMBEDDINGSHIFT_MINIINSURANCE_DATASET_ROOT = \"{suggestedStage0}\"");
        log("  dotnet run --project src/EmbeddingShift.ConsoleEval -- domain mini-insurance pipeline");
        log("");

        return datasetRoot;
    }

    private static List<(string PolicyId, Topic Topic)> BuildBasePolicies(int count)
    {
        // Ensure good coverage across topics (round-robin), deterministic ids.
        var list = new List<(string, Topic)>(count);
        for (var i = 0; i < count; i++)
        {
            var t = Topics[i % Topics.Length];
            var id = $"policy-{t.Key}-{i + 1:000}";
            list.Add((id, t));
        }
        return list;
    }

    private static List<QueryDef> BuildBaseQueries(
        List<(string PolicyId, Topic Topic)> policies,
        int targetQueries,
        Random rng)
    {
        var templates = new[]
        {
            "What is covered under the {TOPIC} section, and what exclusions apply?",
            "What is the deductible (excess) for a typical {TOPIC} claim?",
            "How do I file a claim for {TOPIC} and what evidence is required?",
            "Does the policy cover consequential loss related to {TOPIC} events?",
            "Are emergency measures reimbursed for {TOPIC} incidents?"
        };

        var queries = new List<QueryDef>(targetQueries);
        var q = 1;

        // 1) At least one query per policy (so every doc is “reachable”)
        foreach (var (policyId, topic) in policies)
        {
            var tpl = templates[(q - 1) % templates.Length];
            var text = tpl.Replace("{TOPIC}", topic.Title);
            queries.Add(new QueryDef($"q{q:0000}", text, policyId));
            q++;
            if (queries.Count >= targetQueries) return queries;
        }

        // 2) Fill up to targetQueries with additional, varied queries (still mapped to real policies)
        while (queries.Count < targetQueries)
        {
            var pick = policies[rng.Next(policies.Count)];
            var tpl = templates[rng.Next(templates.Length)];
            var text = tpl.Replace("{TOPIC}", pick.Topic.Title);

            // Add a bit of variance
            text = AddVariantTail(text, rng);

            queries.Add(new QueryDef($"q{q:0000}", text, pick.PolicyId));
            q++;
        }

        return queries;
    }

    private static void WriteStage(
        string stageRoot,
        int stageIndex,
        string stageName,
        List<(string PolicyId, Topic Topic)> basePolicies,
        List<QueryDef> baseQueries,
        Random rng,
        bool addCorpusConfusers,
        int confuserCount,
        bool driftQueries)
    {
        var policiesDir = Path.Combine(stageRoot, "policies");
        var queriesDir = Path.Combine(stageRoot, "queries");
        Directory.CreateDirectory(policiesDir);
        Directory.CreateDirectory(queriesDir);

        // Policies
        var policies = new List<(string PolicyId, Topic Topic)>(basePolicies);

        if (addCorpusConfusers)
        {
            // D1: add confusers that are textually similar but not “relevant” for existing queries.
            for (var i = 0; i < confuserCount; i++)
            {
                var src = basePolicies[(i * 7) % basePolicies.Count];
                var confId = $"policy-{src.Topic.Key}-confuser-{i + 1:000}";
                policies.Add((confId, src.Topic));
            }
        }

        foreach (var (policyId, topic) in policies)
        {
            var isConfuser = policyId.Contains("-confuser-", StringComparison.OrdinalIgnoreCase);
            var text = BuildPolicyText(policyId, topic, rng, isConfuser);
            File.WriteAllText(Path.Combine(policiesDir, policyId + ".txt"), text, Encoding.UTF8);
        }

        // Queries
        var queries = driftQueries
            ? baseQueries.Select(q => q with { Text = ApplyQueryDrift(q.Text, rng) }).ToList()
            : baseQueries.ToList();

        WriteJson(Path.Combine(queriesDir, "queries.json"), queries);

        // Stage manifest
        var stageManifest = new StageManifest(
            StageIndex: stageIndex,
            StageName: stageName,
            Policies: policies.Count,
            Queries: queries.Count,
            CorpusDriftD1: addCorpusConfusers,
            QueryDriftD2: driftQueries);

        WriteJson(Path.Combine(stageRoot, "stage.manifest.json"), stageManifest);
    }

    private static string BuildPolicyText(string policyId, Topic topic, Random rng, bool isConfuser)
    {
        // Intentionally “keyword-rich” so later semantic simulation can pick up signals.
        var sb = new StringBuilder();
        sb.AppendLine($"Policy ID: {policyId}");
        sb.AppendLine($"Title: {topic.Title} Coverage");
        sb.AppendLine("");

        sb.AppendLine("1. Scope of Coverage");
        sb.AppendLine($"This policy provides coverage for {topic.Title.ToLowerInvariant()} related events.");
        sb.AppendLine($"Key terms: {string.Join(", ", topic.Keywords)}.");
        sb.AppendLine("");

        sb.AppendLine("2. Typical Covered Events");
        sb.AppendLine($"Examples include scenarios involving {topic.Keywords[0]}, {topic.Keywords[1]}, and {topic.Keywords[2]}.");
        sb.AppendLine("Emergency measures may be reimbursed if reasonable and documented.");
        sb.AppendLine("");

        sb.AppendLine("3. Exclusions");
        sb.AppendLine("Wear and tear, intentional acts, and pre-existing damage are excluded.");
        if (isConfuser)
        {
            // Confusers share many words but introduce subtle “mismatch” cues.
            sb.AppendLine("Note: This wording is similar to another product line and may differ in deductible and scope.");
        }
        sb.AppendLine("");

        sb.AppendLine("4. Deductible (Excess)");
        sb.AppendLine($"A deductible applies per event. Typical range: {rng.Next(200, 1200)} currency units.");
        sb.AppendLine("");

        sb.AppendLine("5. Claims Procedure");
        sb.AppendLine("Report the incident promptly, provide photos/receipts, and cooperate with assessments.");
        sb.AppendLine("Claims handling may require police reports for theft/burglary-related incidents.");
        sb.AppendLine("");

        return sb.ToString();
    }

    private static string AddVariantTail(string text, Random rng)
    {
        var tails = new[]
        {
            "Please cite the relevant section number.",
            "Include any waiting periods or time limits.",
            "Summarize the decision criteria used by claims handling.",
            "Mention whether preventive measures are reimbursed."
        };
        return text + " " + tails[rng.Next(tails.Length)];
    }

    private static string ApplyQueryDrift(string text, Random rng)
    {
        // D2: change phrasing while keeping intent (simple synonym swaps + minor rewording).
        var swaps = new (string A, string B)[]
        {
            ("deductible", "excess"),
            ("file a claim", "report a loss"),
            ("covered", "included"),
            ("evidence", "documentation"),
            ("policy", "contract"),
            ("exclusions", "limitations"),
            ("incident", "event")
        };

        var drifted = text;

        // Apply 1–3 random swaps
        var k = rng.Next(1, 4);
        for (var i = 0; i < k; i++)
        {
            var (a, b) = swaps[rng.Next(swaps.Length)];
            drifted = drifted.Replace(a, b, StringComparison.OrdinalIgnoreCase);
        }

        // Minor restructuring occasionally
        if (rng.NextDouble() < 0.35)
            drifted = "In short: " + drifted;

        return drifted;
    }

    private static void WriteJson<T>(string path, T value)
    {
        var json = JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json, Encoding.UTF8);
    }
}
