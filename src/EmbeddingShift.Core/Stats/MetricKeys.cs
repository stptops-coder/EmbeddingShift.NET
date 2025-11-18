namespace EmbeddingShift.Core.Stats
{
    /// <summary>
    /// Central metric key definitions used across workflows and tests.
    /// </summary>
    public static class MetricKeys
    {
        public static class Prep
        {
            public const string TotalDocs      = "prep.totalDocs";
            public const string TotalChunks    = "prep.totalChunks";
            public const string AvgChunkLength = "prep.avgChunkLength";
            public const string AvgWhitespace  = "prep.avgWhitespace";
        }

        public static class Ingest
        {
            public const string TotalDocs        = "ingest.totalDocs";
            public const string TotalChunks      = "ingest.totalChunks";
            public const string EmbeddingDim     = "ingest.embeddingDim";
            public const string AvgEmbeddingNorm = "ingest.avgEmbeddingNorm";
        }

        public static class Eval
        {
            public const string Map1  = "map@1";
            public const string Ndcg3 = "ndcg@3";

            public const string Map1Baseline   = "eval.map@1.baseline";
            public const string Ndcg3Baseline  = "eval.ndcg@3.baseline";
            public const string Map1Variant    = "eval.map@1.variant";
            public const string Ndcg3Variant   = "eval.ndcg@3.variant";
            public const string DeltaMap1      = "eval.delta.map@1";
            public const string DeltaNdcg3     = "eval.delta.ndcg@3";
        }
    }
}
