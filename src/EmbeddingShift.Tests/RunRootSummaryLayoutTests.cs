using EmbeddingShift.ConsoleEval.Commands;

namespace EmbeddingShift.Tests;

public sealed class RunRootSummaryLayoutTests
{
    [Fact]
    public async Task DefaultSummaries_WithMultipleTenants_ContainOnlyTheirOwnTenant()
    {
        var runRoot = CreateRoot();
        try
        {
            await MiniInsuranceRunRootSummarizeCommand.RunAsync(new[] { "--runroot=" + runRoot });

            foreach (var tenant in new[] { "tenant-a", "tenant-b" })
            {
                var otherTenant = tenant == "tenant-a" ? "tenant-b" : "tenant-a";
                var report = Path.Combine(runRoot, "results", "insurance", "tenants", tenant,
                    "reports", "summary.txt");
                Assert.True(File.Exists(report));
                var text = File.ReadAllText(report);
                Assert.Contains("Tenant        : " + tenant, text);
                Assert.DoesNotContain("Tenant        : " + otherTenant, text);
                Assert.DoesNotContain(Path.Combine("tenants", otherTenant), text);
            }
        }
        finally
        {
            Directory.Delete(runRoot, recursive: true);
        }
    }

    [Fact]
    public async Task ExplicitOutput_WithMultipleTenants_WritesOneCombinedSummary()
    {
        var runRoot = CreateRoot();
        try
        {
            var report = Path.Combine(runRoot, "combined.txt");
            await MiniInsuranceRunRootSummarizeCommand.RunAsync(new[]
            {
                "--runroot=" + runRoot, "--out=" + report
            });

            var text = File.ReadAllText(report);
            foreach (var tenant in new[] { "tenant-a", "tenant-b" })
            {
                Assert.Contains("Tenant        : " + tenant, text);
                Assert.False(Directory.Exists(Path.Combine(runRoot, "results", "insurance", "tenants",
                    tenant, "reports")));
            }
            Assert.Single(Directory.GetFiles(runRoot, "summary.txt", SearchOption.AllDirectories).Concat(
                Directory.GetFiles(runRoot, "combined.txt", SearchOption.TopDirectoryOnly)));
        }
        finally
        {
            Directory.Delete(runRoot, recursive: true);
        }
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "EmbeddingShift.SummaryLayout", Guid.NewGuid().ToString("N"));
        foreach (var tenant in new[] { "tenant-a", "tenant-b" })
            Directory.CreateDirectory(Path.Combine(root, "results", "insurance", "tenants", tenant, "runs"));
        return root;
    }
}
