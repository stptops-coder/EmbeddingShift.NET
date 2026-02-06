using System;
using System.Collections.Generic;

namespace EmbeddingShift.Core.QueryPolicies
{
    /// <summary>
    /// Query shift policy:
    /// maps QueryId -> list of shift-training result JSON files.
    /// The referenced files are expected to be ShiftTrainingResult JSON artifacts.
    /// </summary>
    public sealed class QueryShiftPolicy
    {
        /// <summary>
        /// Version marker for forward compatibility.
        /// </summary>
        public int Version { get; init; } = 1;

        /// <summary>
        /// Optional free-form name for diagnostics.
        /// </summary>
        public string? Name { get; init; }

        /// <summary>
        /// QueryId -> list of ShiftTrainingResult JSON file paths.
        /// Paths may be absolute or relative to the policy file directory.
        /// </summary>
        public Dictionary<string, List<string>> QueryIdToShiftTrainingResults { get; init; } =
            new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
    }
}
