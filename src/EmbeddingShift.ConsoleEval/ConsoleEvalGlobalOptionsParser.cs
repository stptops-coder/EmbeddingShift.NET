using System;
using System.Collections.Generic;
using System.Linq;
using EmbeddingShift.Abstractions;

namespace EmbeddingShift.ConsoleEval;

public static class ConsoleEvalGlobalOptionsParser
{
    public static ConsoleEvalParsedArgs Parse(string[] args)
    {
        if (args is null) throw new ArgumentNullException(nameof(args));

        var pass = new List<string>(args.Length);
        var opt = new ConsoleEvalGlobalOptions();

        foreach (var a in args.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            if (a.StartsWith("--provider=", StringComparison.OrdinalIgnoreCase))
            {
                opt = opt with { Provider = a.Split('=', 2)[1].Trim() };
                continue;
            }

            if (a.StartsWith("--backend=", StringComparison.OrdinalIgnoreCase))
            {
                opt = opt with { Backend = a.Split('=', 2)[1].Trim() };
                continue;
            }

            // Mode switch: --method=A => identity/no shift
            if (a.Equals("--method=A", StringComparison.OrdinalIgnoreCase))
            {
                opt = opt with { Method = ShiftMethod.NoShiftIngestBased };
                continue;
            }

            if (a.StartsWith("--sim-mode=", StringComparison.OrdinalIgnoreCase))
            {
                opt = opt with { SimMode = a.Substring("--sim-mode=".Length).Trim() };
                continue;
            }

            if (a.StartsWith("--sim-noise=", StringComparison.OrdinalIgnoreCase))
            {
                opt = opt with { SimNoiseAmplitude = a.Substring("--sim-noise=".Length).Trim() };
                continue;
            }

            if (a.StartsWith("--sim-algo=", StringComparison.OrdinalIgnoreCase))
            {
                opt = opt with { SimAlgo = a.Substring("--sim-algo=".Length).Trim() };
                continue;
            }

            if (a.StartsWith("--sim-char-ngrams=", StringComparison.OrdinalIgnoreCase))
            {
                opt = opt with { SimSemanticCharNGrams = a.Substring("--sim-char-ngrams=".Length).Trim() };
                continue;
            }

            if (a.Equals("--semantic-cache", StringComparison.OrdinalIgnoreCase))
            {
                opt = opt with { SemanticCache = true };
                continue;
            }

            if (a.Equals("--no-semantic-cache", StringComparison.OrdinalIgnoreCase))
            {
                opt = opt with { SemanticCache = false };
                continue;
            }

            if (a.StartsWith("--cache-max=", StringComparison.OrdinalIgnoreCase))
            {
                opt = opt with { CacheMax = a.Substring("--cache-max=".Length).Trim() };
                continue;
            }

            if (a.StartsWith("--cache-hamming=", StringComparison.OrdinalIgnoreCase))
            {
                opt = opt with { CacheHamming = a.Substring("--cache-hamming=".Length).Trim() };
                continue;
            }

            if (a.StartsWith("--cache-approx=", StringComparison.OrdinalIgnoreCase))
            {
                opt = opt with { CacheApprox = a.Substring("--cache-approx=".Length).Trim() };
                continue;
            }

            pass.Add(a);
        }

        return new ConsoleEvalParsedArgs(opt, pass.ToArray());
    }
}
