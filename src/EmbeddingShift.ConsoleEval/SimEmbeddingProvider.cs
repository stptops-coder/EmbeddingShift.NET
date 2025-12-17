using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using EmbeddingShift.Abstractions;
using EmbeddingShift.Core.Shifts;
using EmbeddingShift.Simulation;

namespace EmbeddingShift.ConsoleEval;

/// <summary>
/// Simulated embedding provider used when EMBEDDING_BACKEND = "sim".
/// Default algorithm is legacy SHA-256 mapping (deterministic, but no semantic locality).
///
/// To enable a more "embedding-like" simulation:
///   EMBEDDING_SIM_ALGO=semantic-hash
/// Optional:
///   EMBEDDING_SIM_SEMANTIC_CHAR_NGRAMS=1
/// </summary>
public sealed class SimEmbeddingProvider : IEmbeddingProvider
{
    private const int Dim = 1536;

    public string Name => "sim";

    private readonly bool _useNoise;
    private readonly float _noiseAmplitude;
    private readonly Random _rng;

    private readonly EmbeddingSimulationAlgorithm _algorithm;
    private readonly bool _semanticIncludeCharNGrams;

    public SimEmbeddingProvider()
        : this(CreateOptionsFromEnvironment())
    {
    }

    public SimEmbeddingProvider(EmbeddingSimulationOptions options)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));

        _rng = Random.Shared;

        _algorithm = options.Algorithm;
        _semanticIncludeCharNGrams = options.SemanticIncludeCharacterNGrams;

        if (options.Mode == EmbeddingSimulationMode.Noisy && options.NoiseAmplitude > 0f)
        {
            _useNoise = true;
            _noiseAmplitude = options.NoiseAmplitude;
        }
        else
        {
            _useNoise = false;
            _noiseAmplitude = 0f;
        }
    }

    public Task<float[]> GetEmbeddingAsync(string text)
    {
        var input = text ?? string.Empty;

        float[] vec = _algorithm switch
        {
            EmbeddingSimulationAlgorithm.SemanticHash => SemanticHashEmbedding.Create(
                text: input,
                embeddingSize: Dim,
                includeCharNGrams: _semanticIncludeCharNGrams),

            _ => CreateLegacySha256Embedding(input)
        };

        if (_useNoise && _noiseAmplitude > 0f)
        {
            var shift = new RandomNoiseShift(_noiseAmplitude, _rng);
            shift.ApplyInPlace(vec);
        }

        return Task.FromResult(vec);
    }

    private static float[] CreateLegacySha256Embedding(string text)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));

        var vec = new float[Dim];
        for (int i = 0; i < Dim; i++)
        {
            vec[i] = (bytes[i % bytes.Length] / 255f) - 0.5f;
        }

        return vec;
    }

    private static EmbeddingSimulationOptions CreateOptionsFromEnvironment()
    {
        var modeEnv = Environment.GetEnvironmentVariable("EMBEDDING_SIM_MODE");

        var algorithm = ParseAlgorithmFromEnvironment();
        var includeCharNGrams = ParseTruthyEnvironment("EMBEDDING_SIM_SEMANTIC_CHAR_NGRAMS");

        if (string.Equals(modeEnv, "noisy", StringComparison.OrdinalIgnoreCase))
        {
            var amplitudeEnv = Environment.GetEnvironmentVariable("EMBEDDING_SIM_NOISE_AMPLITUDE");

            if (!float.TryParse(amplitudeEnv, NumberStyles.Float, CultureInfo.InvariantCulture, out var amplitude))
                amplitude = 0.05f;

            return new EmbeddingSimulationOptions
            {
                Mode = EmbeddingSimulationMode.Noisy,
                NoiseAmplitude = amplitude,
                Algorithm = algorithm,
                SemanticIncludeCharacterNGrams = includeCharNGrams
            };
        }

        return new EmbeddingSimulationOptions
        {
            Mode = EmbeddingSimulationMode.Deterministic,
            NoiseAmplitude = 0f,
            Algorithm = algorithm,
            SemanticIncludeCharacterNGrams = includeCharNGrams
        };
    }

    private static EmbeddingSimulationAlgorithm ParseAlgorithmFromEnvironment()
    {
        var algoEnv = (Environment.GetEnvironmentVariable("EMBEDDING_SIM_ALGO") ?? "sha256").Trim();
        if (string.IsNullOrWhiteSpace(algoEnv)) algoEnv = "sha256";

        return algoEnv.ToLowerInvariant() switch
        {
            "semantic" or "semhash" or "semantic-hash" or "semantichash" => EmbeddingSimulationAlgorithm.SemanticHash,
            _ => EmbeddingSimulationAlgorithm.Sha256
        };
    }

    private static bool ParseTruthyEnvironment(string variableName)
    {
        var v = (Environment.GetEnvironmentVariable(variableName) ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(v)) return false;

        return v.Equals("1", StringComparison.OrdinalIgnoreCase)
               || v.Equals("true", StringComparison.OrdinalIgnoreCase)
               || v.Equals("yes", StringComparison.OrdinalIgnoreCase)
               || v.Equals("on", StringComparison.OrdinalIgnoreCase);
    }
}
