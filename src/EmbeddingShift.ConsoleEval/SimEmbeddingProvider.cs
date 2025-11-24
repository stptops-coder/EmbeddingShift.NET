using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using EmbeddingShift.Abstractions;
using EmbeddingShift.Core.Shifts;

namespace EmbeddingShift.ConsoleEval;

public sealed class SimEmbeddingProvider : IEmbeddingProvider
{
    public string Name => "sim";
    private const int Dim = 1536;

    private readonly bool _useNoise;
    private readonly float _noiseAmplitude;
    private readonly Random _rng;

    public SimEmbeddingProvider()
    {
        _rng = Random.Shared;

        var mode = Environment.GetEnvironmentVariable("EMBEDDING_SIM_MODE");
        if (string.Equals(mode, "noisy", StringComparison.OrdinalIgnoreCase))
        {
            _useNoise = true;

            var amplitudeEnv = Environment.GetEnvironmentVariable("EMBEDDING_SIM_NOISE_AMPLITUDE");
            if (!float.TryParse(
                    amplitudeEnv,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out _noiseAmplitude))
            {
                // Sensible default for noisy simulation.
                _noiseAmplitude = 0.05f;
            }

            if (_noiseAmplitude < 0f)
            {
                _noiseAmplitude = 0f;
            }
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
}
