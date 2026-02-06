using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using EmbeddingShift.Abstractions.Shifts;
using EmbeddingShift.Core.QueryPolicies;

namespace EmbeddingShift.Core.Shifts
{
    /// <summary>
    /// Applies query-specific delta vectors based on a QueryShiftPolicy.
    /// This shift is intentionally a no-op for documents (no QueryId context)
    /// and only activates when QueryShiftContext.CurrentQueryId is set.
    /// </summary>
    public sealed class QueryPolicyShift : IEmbeddingShift
    {
        private readonly QueryShiftPolicy _policy;
        private readonly Dictionary<string, float[][]> _deltaCache = new Dictionary<string, float[][]>(StringComparer.OrdinalIgnoreCase);
        private readonly object _gate = new object();

        private static readonly JsonSerializerOptions _json = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        public QueryPolicyShift(string name, QueryShiftPolicy policy, float weight = 1.0f)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "QueryPolicy" : name;
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            Weight = weight;
        }

        public string Name { get; }

        public ShiftStage Stage => ShiftStage.Delta;

        public float Weight { get; }

        public void ApplyInPlace(float[] embedding)
        {
            if (embedding == null)
                throw new ArgumentNullException(nameof(embedding));

            if (Weight == 0.0f)
                return;

            var qid = QueryShiftContext.CurrentQueryId;
            if (string.IsNullOrWhiteSpace(qid))
                return;

            if (_policy.QueryIdToShiftTrainingResults == null ||
                !_policy.QueryIdToShiftTrainingResults.TryGetValue(qid, out var files) ||
                files == null || files.Count == 0)
                return;

            var deltas = GetOrLoadDeltas(qid, files);

            for (int d = 0; d < deltas.Length; d++)
            {
                var delta = deltas[d];
                if (delta.Length != embedding.Length)
                    throw new InvalidOperationException(
                        $"DeltaVector length mismatch for QueryId='{qid}'. Expected={embedding.Length}, Actual={delta.Length}");

                for (int i = 0; i < embedding.Length; i++)
                {
                    embedding[i] += Weight * delta[i];
                }
            }
        }

        private float[][] GetOrLoadDeltas(string queryId, List<string> files)
        {
            lock (_gate)
            {
                if (_deltaCache.TryGetValue(queryId, out var cached))
                    return cached;

                var loaded = new List<float[]>();

                foreach (var f in files)
                {
                    var result = LoadShiftTrainingResult(f);

                    // Cancelled candidates are treated as "do nothing" to keep reruns stable.
                    if (result.IsCancelled)
                        continue;

                    if (result.DeltaVector == null)
                        continue;

                    loaded.Add(result.DeltaVector);
                }

                var arr = loaded.ToArray();
                _deltaCache[queryId] = arr;
                return arr;
            }
        }

        private static ShiftTrainingResult LoadShiftTrainingResult(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("ShiftTrainingResult file not found.", path);

            var json = File.ReadAllText(path);
            var result = JsonSerializer.Deserialize<ShiftTrainingResult>(json, _json);

            if (result is null)
                throw new InvalidOperationException($"Failed to deserialize ShiftTrainingResult: {path}");

            return result;
        }

        private sealed class ShiftTrainingResult
        {
            public string? WorkflowName { get; set; }
            public string? RunId { get; set; }
            public bool IsCancelled { get; set; }
            public float[]? DeltaVector { get; set; }
            public Dictionary<string, double>? Metrics { get; set; }
        }
    }
}
