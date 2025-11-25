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
/// By default it behaves deterministically, but it can optionally apply
/// additional random noise when configured in Noisy mode.
/// </summary>
public sealed class SimEmbeddingProvider : IEmbeddingProvider
{
    private const int Dim = 1536;

    public string Name => "sim";

    private readonly bool _useNoise;
    private readonly float _noiseAmplitude;
    private readonly Random _rng;

    /// <summary>
    /// Creates a simulated embedding provider using options derived from
    /// environment variables. This preserves the existing behavior:
    ///
    /// - EMBEDDING_SIM_MODE not set or not "noisy"  -> Deterministic
    /// - EMBEDDING_SIM_MODE = "noisy"               -> Noisy mode with
    ///   amplitude taken from EMBEDDING_SIM_NOISE_AMPLITUDE (or a default).
    /// </summary>
    public SimEmbeddingProvider()
        : this(CreateOptionsFromEnvironment())
    {
    }

    /// <summary>
    /// Creates a simulated embedding provider using explicit simulation options.
    /// This is the preferred entry point for code and UI-driven configuration.
    /// </summary>
    public SimEmbeddingProvider(EmbeddingSimulationOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        _rng = Random.Shared;

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
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty));

        var vec = new float[Dim];
        for (int i = 0; i < Dim; i++)
        {
            vec[i] = (bytes[i % bytes.Length] / 255f) - 0.5f;
        }

        if (_useNoise && _noiseAmplitude > 0f)
        {
            var shift = new RandomNoiseShift(_noiseAmplitude, _rng);
            shift.ApplyInPlace(vec);
        }

        return Task.FromResult(vec);
    }

    private static EmbeddingSimulationOptions CreateOptionsFromEnvironment()
    {
        var modeEnv = Environment.GetEnvironmentVariable("EMBEDDING_SIM_MODE");

        if (string.Equals(modeEnv, "noisy", StringComparison.OrdinalIgnoreCase))
        {
            var amplitudeEnv = Environment.GetEnvironmentVariable("EMBEDDING_SIM_NOISE_AMPLITUDE");

            if (!float.TryParse(
                    amplitudeEnv,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var amplitude))
            {
                // Sensible default for noisy simulation if parsing fails or is not set.
                amplitude = 0.05f;
            }

            if (amplitude < 0f)
            {
                amplitude = 0f;
            }

            return new EmbeddingSimulationOptions
            {
                Mode = EmbeddingSimulationMode.Noisy,
                NoiseAmplitude = amplitude
            };
        }

        // Default: deterministic simulation with no additional noise.
        return new EmbeddingSimulationOptions
        {
            Mode = EmbeddingSimulationMode.Deterministic,
            NoiseAmplitude = 0f
        };
    }
}
