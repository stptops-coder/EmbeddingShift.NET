using System.Threading.Tasks;
using EmbeddingShift.Core.Workflows;
using EmbeddingShift.Workflows;
using Xunit;

using EmbeddingShift.Core.Stats;
namespace EmbeddingShift.Tests
{
    /// <summary>
    /// First mini system integration test:
    /// runs preprocessing, ingest and semantic retrieval workflows
    /// via StatsAwareWorkflowRunner and checks basic consistency.
    /// </summary>
    public class MiniSystemIntegrationTests
    {
        [Fact]
        public async Task End_to_end_mini_system_runs_core_workflows()
        {
            var runner = new StatsAwareWorkflowRunner();

            // 1) Preprocessing: cleanup + chunking + stats
            IWorkflow prepWorkflow = new MiniPreprocessingWorkflow();
            var prepArtifacts = await runner.ExecuteAsync("Mini-Preprocessing-IT", prepWorkflow);

            Assert.True(prepArtifacts.Success);
            Assert.NotNull(prepArtifacts.Metrics);
            Assert.True(prepArtifacts.Metrics.TryGetValue(MetricKeys.Prep.TotalDocs, out var prepDocs));
            Assert.True(prepArtifacts.Metrics.TryGetValue(MetricKeys.Prep.TotalChunks, out var prepChunks));

            // 2) Ingest: cleanup + chunking + simulated embeddings
            IWorkflow ingestWorkflow = new MiniIngestWorkflow();
            var ingestArtifacts = await runner.ExecuteAsync("Mini-Ingest-IT", ingestWorkflow);

            Assert.True(ingestArtifacts.Success);
            Assert.NotNull(ingestArtifacts.Metrics);
            Assert.True(ingestArtifacts.Metrics.TryGetValue(MetricKeys.Ingest.TotalDocs, out var ingestDocs));
            Assert.True(ingestArtifacts.Metrics.TryGetValue(MetricKeys.Ingest.TotalChunks, out var ingestChunks));

            // 3) Semantic retrieval: mini MAP / nDCG scenario
            IWorkflow retrievalWorkflow = new MiniSemanticRetrievalWorkflow();
            var retrievalArtifacts = await runner.ExecuteAsync("Mini-Retrieval-IT", retrievalWorkflow);

            Assert.True(retrievalArtifacts.Success);
            Assert.NotNull(retrievalArtifacts.Metrics);
            Assert.True(retrievalArtifacts.Metrics.TryGetValue(MetricKeys.Eval.Map1, out var map));
            Assert.True(retrievalArtifacts.Metrics.TryGetValue(MetricKeys.Eval.Ndcg3, out var ndcg));

            // Basic cross-workflow consistency checks:
            // - preprocessing and ingest see the same number of documents
            // - ingest sees at least as many chunks as preprocessing
            Assert.InRange(prepDocs,    1.0, 10.0);
            Assert.InRange(ingestDocs,  1.0, 10.0);
            Assert.Equal(prepDocs, ingestDocs);

            Assert.True(prepChunks  >= prepDocs);
            Assert.True(ingestChunks >= ingestDocs);

            // Retrieval metrics should be valid probabilities in [0,1]
            Assert.InRange(map,  0.0, 1.0);
            Assert.InRange(ndcg, 0.0, 1.0);
        }
    }
}

