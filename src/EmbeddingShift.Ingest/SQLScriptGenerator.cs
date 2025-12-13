using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace EmbeddingShift.Ingest
{
    public static class SQLScriptGenerator
    {
        /// <summary>
        /// Writes a simple INSERT statement into the file (append).
        /// Strings are quoted correctly ('' escaping), numbers are culture-invariant.
        /// </summary>
        public static void WriteInsert(string path, string table, IDictionary<string, object> values)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("path is null/empty", nameof(path));
            if (string.IsNullOrWhiteSpace(table)) throw new ArgumentException("table is null/empty", nameof(table));
            if (values is null || values.Count == 0) throw new ArgumentException("values empty", nameof(values));

            var cols = string.Join(",", values.Keys.Select(BracketIfNeeded));
            var vals = string.Join(",", values.Values.Select(FormatValue));

            var line = $"INSERT INTO {BracketIfNeeded(table)} ({cols}) VALUES ({vals});{Environment.NewLine}";
            File.AppendAllText(path, line);
        }

        private static string FormatValue(object? v)
        {
            switch (v)
            {
                case null:
                    return "NULL";
                case string s:
                    return $"'{s.Replace("'", "''")}'";
                case bool b:
                    return b ? "1" : "0";
                case IFormattable f:
                    return f.ToString(null, CultureInfo.InvariantCulture);
                default:
                    return $"'{v.ToString()!.Replace("'", "''")}'";
            }
        }

        private static string BracketIfNeeded(string identifier)
        {
            // Simple guard for potential reserved words or identifiers with spaces/symbols.
            if (identifier.Any(ch => !char.IsLetterOrDigit(ch) && ch != '_'))
                return $"[{identifier}]";
            return identifier;
        }
    }
}

