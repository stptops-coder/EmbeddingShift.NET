using System.Diagnostics;
using System.Reflection;
using EmbeddingShift.Core.Infrastructure;
using EmbeddingShift.Core.Runs;

namespace EmbeddingShift.ConsoleEval.Commands
{
    public static class RunsMatrixCommand
    {
        // Usage:
        //   runs-matrix --spec=<path> [--runs-root=<path>] [--domainKey=<key>] [--dry] [--open]
        //
        // The spec file contains the list of variants (each variant is a CLI argument array for this ConsoleEval app)
        // plus optional "after" settings (compare/promote/open).

        public static async Task RunAsync(string[] args)
        {
            var specPath = GetOpt(args, "--spec");
            var runsRoot = GetOpt(args, "--runs-root");

            var domainKeyOverride = GetOpt(args, "--domainKey");
            var dry = HasSwitch(args, "--dry");
            var openOverride = HasSwitch(args, "--open");

            if (string.IsNullOrWhiteSpace(specPath))
            {
                PrintUsage();
                return;
            }

            var tenant =
                 GetOpt(args, "--tenant")
                 ?? Environment.GetEnvironmentVariable("EMBEDDINGSHIFT_TENANT")
                 ?? throw new InvalidOperationException("Tenant is required (use --tenant).");

            // Ensure the current process has the tenant set.
            // Child processes started by runs-matrix will inherit this by default.
            Environment.SetEnvironmentVariable("EMBEDDINGSHIFT_TENANT", tenant);

            var domainKey = domainKeyOverride ?? "insurance";

            runsRoot ??= Path.Combine(
                Environment.CurrentDirectory,
                "results",
                domainKey,
                "tenants",
                tenant,
                "runs");

            var outDir = Path.Combine(runsRoot, "_matrix", $"matrix_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}");
            Directory.CreateDirectory(outDir);

            var spec = RunMatrixSpec.Load(specPath);
            var after = spec.After ?? new RunMatrixAfter();

            // DomainKey can be defined either via CLI or spec.
            var effectiveDomainKey = domainKeyOverride ?? after.DomainKey ?? domainKey;

            Console.WriteLine($"[runs-matrix] tenant   = {tenant}");
            Console.WriteLine($"[runs-matrix] domain   = {effectiveDomainKey}");
            Console.WriteLine($"[runs-matrix] runsRoot = {runsRoot}");
            Console.WriteLine($"[runs-matrix] outDir   = {outDir}");
            Console.WriteLine($"[runs-matrix] variants = {spec.Variants.Count}");
            Console.WriteLine(dry ? "[runs-matrix] mode    = DRY-RUN (no processes executed)" : "[runs-matrix] mode    = EXECUTE");

            var entryDll = GetEntryAssemblyPath();

            var results = new List<(string Name, int ExitCode, TimeSpan Duration, string LogPath)>();

            foreach (var variant in spec.Variants)
            {
                var safeName = SanitizeFileName(variant.DisplayName);
                var logPath = Path.Combine(outDir, $"{safeName}.log");

                Console.WriteLine();
                Console.WriteLine($"[runs-matrix] Variant: {variant.DisplayName}");
                Console.WriteLine($"[runs-matrix]   Log  : {logPath}");

                var explicitTenant = TryFindExplicitTenantInArgs(variant.Args);
                if (!string.IsNullOrWhiteSpace(explicitTenant) &&
                    !string.Equals(explicitTenant, tenant, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"[runs-matrix]   WARN : Variant specifies tenant '{explicitTenant}', matrix tenant is '{tenant}'.");
                    Console.WriteLine("[runs-matrix]          This may write runs to a different folder than runsRoot/post-processing.");
                }

                if (dry)
                {
                    var runner = entryDll.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                        ? $"\"{entryDll}\""
                        : $"dotnet \"{entryDll}\"";
                    Console.WriteLine($"[runs-matrix]   Cmd  : {runner} {string.Join(" ", variant.Args)}");
                    continue;
                }

                var sw = Stopwatch.StartNew();

                var exitCode = await RunVariantAsync(entryDll, variant, logPath, tenant);

                sw.Stop();
                results.Add((variant.DisplayName, exitCode, sw.Elapsed, logPath));

                Console.WriteLine($"[runs-matrix]   Exit : {exitCode}");
                Console.WriteLine($"[runs-matrix]   Time : {sw.Elapsed}");

                if (exitCode != 0 && spec.StopOnFailure)
                {
                    Console.WriteLine("[runs-matrix] StopOnFailure=true → aborting remaining variants.");
                    break;
                }
            }

            if (dry)
            {
                if (openOverride || after.OpenOutput)
                    OpenFolder(outDir);

                return;
            }

            // Post-processing (compare/promote/open).
            if (after.WriteCompare || after.PromoteBest)
            {
                Console.WriteLine();
                Console.WriteLine("[runs-matrix] Post-processing: runs-compare");
                var compareArgs = new List<string>
                {
                    $"--runs-root={runsRoot}",
                    $"--domainKey={effectiveDomainKey}",
                    $"--metric={after.CompareMetric}",
                    $"--top={after.Top}",
                    $"--out={outDir}"
                };

                if (after.WriteCompare)
                    compareArgs.Add("--write");

                await RunsCompareCommand.RunAsync(compareArgs.ToArray());
            }

            if (after.PromoteBest)
            {
                Console.WriteLine();
                Console.WriteLine("[runs-matrix] Post-processing: runs-promote");
                var promoteArgs = new[]
                {
                    $"--runs-root={runsRoot}",
                    $"--domainKey={effectiveDomainKey}",
                    $"--metric={after.CompareMetric}"
                };

                await RunsPromoteCommand.RunAsync(promoteArgs);
            }

            WriteSummary(outDir, results);

            if (openOverride || after.OpenOutput)
                OpenFolder(outDir);
        }

        private static async Task<int> RunVariantAsync(string entryDll, RunMatrixVariant variant, string logPath, string matrixTenant)
        {
            var isExe = entryDll.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

            var psi = new ProcessStartInfo(isExe ? entryDll : "dotnet")
            {
                WorkingDirectory = Environment.CurrentDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            if (!isExe)
                psi.ArgumentList.Add(entryDll);

            foreach (var a in variant.Args ?? Array.Empty<string>())
                psi.ArgumentList.Add(a);

            if (variant.Env is not null)
            {
                foreach (var kvp in variant.Env)
                    psi.Environment[kvp.Key] = kvp.Value;
            }

            // Ensure a tenant is present for child processes, unless a variant explicitly sets it via ENV.
            if (!psi.Environment.ContainsKey("EMBEDDINGSHIFT_TENANT") ||
                string.IsNullOrWhiteSpace(psi.Environment["EMBEDDINGSHIFT_TENANT"]))
            {
                psi.Environment["EMBEDDINGSHIFT_TENANT"] = matrixTenant;
            }

            using var p = new Process { StartInfo = psi };

            p.Start();

            var stdOutTask = p.StandardOutput.ReadToEndAsync();
            var stdErrTask = p.StandardError.ReadToEndAsync();

            if (variant.TimeoutSeconds is { } timeoutSeconds && timeoutSeconds > 0)
            {
                var waitTask = p.WaitForExitAsync();
                var completed = await Task.WhenAny(waitTask, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds)));
                if (completed != waitTask)
                {
                    try { p.Kill(entireProcessTree: true); } catch { /* best effort */ }
                    await File.AppendAllTextAsync(logPath, $"[runs-matrix] TIMEOUT after {timeoutSeconds}s\n");
                    return 124; // common timeout exit code
                }
            }
            else
            {
                await p.WaitForExitAsync();
            }

            var stdOut = await stdOutTask;
            var stdErr = await stdErrTask;

            var combined = stdOut + (string.IsNullOrWhiteSpace(stdErr) ? "" : "\n[STDERR]\n" + stdErr);
            await File.WriteAllTextAsync(logPath, combined);

            // Also print a short tail to console (keeps matrix runs readable).
            PrintTailToConsole(combined, maxLines: 40);

            return p.ExitCode;
        }

        private static void WriteSummary(string outDir, List<(string Name, int ExitCode, TimeSpan Duration, string LogPath)> results)
        {
            var path = Path.Combine(outDir, "_summary.txt");

            using var sw = new StreamWriter(path, append: false);

            sw.WriteLine("runs-matrix summary");
            sw.WriteLine($"utc: {DateTime.UtcNow:O}");
            sw.WriteLine();

            foreach (var r in results)
                sw.WriteLine($"- {r.Name} | exit={r.ExitCode} | time={r.Duration} | log={r.LogPath}");

            Console.WriteLine();
            Console.WriteLine($"[runs-matrix] Wrote: {path}");
        }

        private static string GetEntryAssemblyPath()
        {
            var entry = Assembly.GetEntryAssembly();
            var path = entry?.Location;

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new InvalidOperationException("Cannot determine entry assembly path (ConsoleEval dll).");

            return path;
        }

        private static void OpenFolder(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[runs-matrix] Failed to open folder: {ex.Message}");
            }
        }

        private static void PrintTailToConsole(string text, int maxLines)
        {
            var lines = (text ?? "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var start = Math.Max(0, lines.Length - maxLines);

            Console.WriteLine("[runs-matrix] --- output tail ---");
            for (var i = start; i < lines.Length; i++)
                Console.WriteLine(lines[i]);
            Console.WriteLine("[runs-matrix] -------------------");
        }

        private static string? GetOpt(string[] args, string name)
        {
            // Supports both "--name=value" and "--name value".
            for (var i = 0; i < args.Length; i++)
            {
                var a = args[i];

                if (a.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
                    return a[(name.Length + 1)..];

                if (a.Equals(name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    return args[i + 1];
            }

            return null;
        }

        private static bool HasSwitch(string[] args, string name)
            => args.Any(a => a.Equals(name, StringComparison.OrdinalIgnoreCase));

        private static string SanitizeFileName(string name)
        {
            var safe = name;
            foreach (var c in Path.GetInvalidFileNameChars())
                safe = safe.Replace(c, '_');

            safe = safe.Trim();
            return string.IsNullOrWhiteSpace(safe) ? "variant" : safe;
        }

        private static string? TryFindExplicitTenantInArgs(string[]? args)
        {
            if (args is null || args.Length == 0)
                return null;

            for (var i = 0; i < args.Length; i++)
            {
                var a = args[i] ?? string.Empty;

                if (a.StartsWith("--tenant=", StringComparison.OrdinalIgnoreCase))
                    return a["--tenant=".Length..];

                if (a.Equals("--tenant", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    return args[i + 1];
            }

            return null;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("  runs-matrix --spec=<path> [--runs-root=<path>] [--domainKey=<key>] [--dry] [--open]");
            Console.WriteLine();
            Console.WriteLine("Notes:");
            Console.WriteLine("  - The spec file is a JSON file (see example below).");
            Console.WriteLine("  - Variants are executed as separate processes.");
            Console.WriteLine("  - Results remain under results/<domainKey>/tenants/<tenant>/runs as usual.");
            Console.WriteLine();
            Console.WriteLine("Example spec:");
            Console.WriteLine("""
            {
              "stopOnFailure": true,
              "variants": [
                {
                  "name": "sha256",
                  "args": [ "--backend=sim", "--sim-mode=deterministic", "domain", "mini-insurance", "pipeline" ]
                },
                {
                  "name": "semantic-hash-1gram",
                  "args": [ "--backend=sim", "--sim-mode=deterministic", "--sim-algo=semantic-hash", "--sim-char-ngrams=1", "--semantic-cache", "domain", "mini-insurance", "pipeline" ]
                }
              ],
              "after": {
                "compareMetric": "ndcg@3",
                "top": 10,
                "writeCompare": true,
                "promoteBest": false,
                "openOutput": false,
                "domainKey": "insurance"
              }
            }
            """);
        }
    }
}
