using System;
using System.Linq;
using System.Reflection;
using System.Text;

namespace EmbeddingShift.Core.Workflows
{
    /// <summary>
    /// Helper extensions that turn <see cref="WorkflowResult"/> into
    /// a simple textual / markdown representation for tests.
    /// </summary>
    public static class WorkflowResultMarkdownExtensions
    {
        public static string Workflow(this WorkflowResult result)
        {
            if (result is null) return string.Empty;

            var type = result.GetType();

            var nameProp =
                type.GetProperty("WorkflowName", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance) ??
                type.GetProperty("Name", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance) ??
                type.GetProperty("Id", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            var value = nameProp?.GetValue(result);
            return value?.ToString() ?? type.Name;
        }

        public static string ReportMarkdown(this WorkflowResult result)
        {
            if (result is null) return string.Empty;

            var sb = new StringBuilder();
            var type = result.GetType();

            sb.AppendLine($"# Workflow: {result.Workflow()}");
            sb.AppendLine();

            var props = type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead);

            foreach (var prop in props)
            {
                var value = prop.GetValue(result);
                sb.AppendLine($"- **{prop.Name}**: {value}");
            }

            return sb.ToString();
        }
    }
}
