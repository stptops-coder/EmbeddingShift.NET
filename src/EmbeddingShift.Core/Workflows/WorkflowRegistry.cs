using System;
using System.Collections.Generic;
using System.Linq;

namespace EmbeddingShift.Core.Workflows
{
    /// <summary>
    /// Simple in-memory registry for named workflows.
    /// Later we can wire this into the CLI or UI.
    /// </summary>
    public sealed class WorkflowRegistry
    {
        private readonly Dictionary<string, Func<IWorkflow>> _factories =
            new(StringComparer.OrdinalIgnoreCase);

        public WorkflowRegistry Register(string key, Func<IWorkflow> factory)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key must not be null or whitespace.", nameof(key));
            _factories[key] = factory ?? throw new ArgumentNullException(nameof(factory));
            return this;
        }

        public IWorkflow Resolve(string key)
        {
            if (!_factories.TryGetValue(key, out var factory))
                throw new InvalidOperationException($"Workflow not found: {key}");
            return factory();
        }

        public IReadOnlyList<string> Keys =>
            _factories.Keys
                .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }
}
