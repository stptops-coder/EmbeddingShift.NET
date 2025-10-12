using EmbeddingShift.Abstractions;

namespace EmbeddingShift.Adaptive
{
    /// <summary>
    /// Extensible composite: combines multiple IShiftGenerators and applies optional
    /// post-processing (filters, distinct, limit) via a fluent Builder.
    /// </summary>
    public sealed class CompositeShiftGenerator : IShiftGenerator
    {
        private readonly IReadOnlyList<IShiftGenerator> _generators;
        private readonly IReadOnlyList<Func<IShift, bool>> _filters;
        private readonly bool _distinct;
        private readonly IEqualityComparer<IShift> _equalityComparer;
        private readonly int? _limit;

        private CompositeShiftGenerator(
            IReadOnlyList<IShiftGenerator> generators,
            IReadOnlyList<Func<IShift, bool>> filters,
            bool distinct,
            IEqualityComparer<IShift> equalityComparer,
            int? limit)
        {
            _generators = generators;
            _filters = filters;
            _distinct = distinct;
            _equalityComparer = equalityComparer;
            _limit = limit;
        }

        public IEnumerable<IShift> Generate(
            IReadOnlyList<(ReadOnlyMemory<float> Query, ReadOnlyMemory<float> Answer)> pairs)
        {
            IEnumerable<IShift> stream = Enumerable.Empty<IShift>();

            // 1) Concatenate all candidates
            foreach (var g in _generators)
            {
                stream = stream.Concat(g.Generate(pairs));
            }

            // 2) Apply filters (if any)
            foreach (var f in _filters)
                stream = stream.Where(f);

            // 3) Distinct (optional)
            if (_distinct)
                stream = stream.Distinct(_equalityComparer);

            // 4) Limit (optional)
            if (_limit.HasValue)
                stream = stream.Take(_limit.Value);

            return stream;
        }

        // ---------- Fluent Builder ----------
        public static Builder Create() => new();

        public sealed class Builder
        {
            private readonly List<IShiftGenerator> _generators = new();
            private readonly List<Func<IShift, bool>> _filters = new();
            private bool _distinct;
            private IEqualityComparer<IShift> _comparer = new ShiftDefaultComparer();
            private int? _limit;

            /// <summary>Add one or many generators.</summary>
            public Builder Add(params IShiftGenerator[] generators)
            {
                if (generators is { Length: > 0 }) _generators.AddRange(generators);
                return this;
            }

            /// <summary>Register a post-filter (e.g., norm caps, type gating, etc.).</summary>
            public Builder WithFilter(Func<IShift, bool> predicate)
            {
                _filters.Add(predicate);
                return this;
            }

            /// <summary>Ensure distinct candidates (by comparer).</summary>
            public Builder WithDistinct(IEqualityComparer<IShift>? comparer = null)
            {
                _distinct = true;
                if (comparer != null) _comparer = comparer;
                return this;
            }

            /// <summary>Cap the number of candidates to evaluate.</summary>
            public Builder WithLimit(int maxCandidates)
            {
                _limit = Math.Max(1, maxCandidates);
                return this;
            }

            public CompositeShiftGenerator Build()
            {
                if (_generators.Count == 0)
                    throw new InvalidOperationException("Composite needs at least one generator.");
                return new CompositeShiftGenerator(_generators, _filters, _distinct, _comparer, _limit);
            }
        }

        // Default equality: prefer IIdentifiedShift.Id; else (Type, HashCode)
        private sealed class ShiftDefaultComparer : IEqualityComparer<IShift>
        {
            public bool Equals(IShift? x, IShift? y)
            {
                if (ReferenceEquals(x, y)) return true;
                if (x is null || y is null) return false;

                if (x is IIdentifiedShift xi && y is IIdentifiedShift yi)
                    return xi.Id == yi.Id;

                return x.GetType() == y.GetType() && x.GetHashCode() == y.GetHashCode();
            }

            public int GetHashCode(IShift obj)
            {
                if (obj is IIdentifiedShift id) return id.Id.GetHashCode();
                return HashCode.Combine(obj.GetType(), obj.GetHashCode());
            }
        }
    }

    /// <summary>
    /// Optional: if a shift can expose a stable identity (for dedupe).
    /// Implement on your Additive/Multiplicative shifts or generators.
    /// </summary>
    public interface IIdentifiedShift : IShift
    {
        string Id { get; }
    }
}
