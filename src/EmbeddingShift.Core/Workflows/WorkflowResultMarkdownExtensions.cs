using System;
using System.Text;

namespace EmbeddingShift.Core.Workflows
{
    /// <summary>
    /// Markdown helpers for <see cref="WorkflowResult"/>.
    /// Central place for all workflow-related Markdown formatting.
    /// </summary>
    public static class WorkflowResultMarkdownExtensions
    {
        /// <summary>
        /// Builds a simple markdown representation of the given workflow result.
        /// The <paramref name="title"/> can be used to override the main heading,
        /// e.g. "Evaluation", "Run Statistics", etc.
        /// </summary>
        public static string ToMarkdown(this WorkflowResult result, string title = "Workflow")
        {
            if (result is null) throw new ArgumentNullException(nameof(result));

            var sb = new StringBuilder();

            // Very conservative: we do not rely on any specific properties
            // of WorkflowResult here, so this stays compile-safe even if
            // the type evolves.
            sb.AppendLine($"# {title}");
            sb.AppendLine();
            sb.AppendLine($"Generated at: {DateTimeOffset.UtcNow:O}");

            return sb.ToString();
        }

        /// <summary>
        /// Backwards-compatible alias for older code that still calls
        /// result.ReportMarkdown(...). Uses <see cref="ToMarkdown"/> internally.
        /// </summary>
        public static string ReportMarkdown(this WorkflowResult result, string title = "Workflow")
            => result.ToMarkdown(title);

        /// <summary>
        /// Backwards-compatible alias for older code that still calls
        /// result.Workflow(). Currently just calls <see cref="ToMarkdown"/>.
        /// </summary>
        public static string Workflow(this WorkflowResult result, string title = "Workflow")
            => result.ToMarkdown(title);
    }
}
