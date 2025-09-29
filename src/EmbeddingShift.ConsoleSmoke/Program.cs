using EmbeddingShift.Abstractions;
using EmbeddingShift.Core.Generators;
using EmbeddingShift.Adaptive;

internal class Program
{
    private const int DIM = EmbeddingDimensions.DIM;

    private static void Main(string[] args)
    {
        Console.WriteLine("=== Shift Evaluation Demo (1536-dim) ===");

        // --- Dummy data: Query + Answer embeddings ---
        var rnd = new Random();

        float[] query = RandomVector(rnd);
        float[] answer1 = RandomVector(rnd);
        float[] answer2 = RandomVector(rnd);

        var pairs = new List<(ReadOnlyMemory<float>, ReadOnlyMemory<float>)>
        {
            (query, answer1),
            (query, answer2)
        };

        // --- Use CompositeShiftGenerator (Additive + Multiplicative) ---
        var generator = CompositeShiftGenerator.Create()
            .Add(new DeltaShiftGenerator(), new MultiplicativeShiftGenerator())
            .WithDistinct()
            .WithLimit(20)
            .Build();

        // --- Run evaluation ---
        var service = new ShiftEvaluationService(generator);
        var report = service.Evaluate(pairs);

        // --- Print results ---
        foreach (var result in report.Results)
        {
            Console.WriteLine($"Evaluator : {result.Evaluator}");
            Console.WriteLine($"  Score   : {result.Score:F4}");
            Console.WriteLine($"  BestShift: {result.BestShift?.GetType().Name ?? "none"}");
            Console.WriteLine();
        }

        Console.WriteLine("Done.");
    }

    private static float[] RandomVector(Random rnd)
    {
        var vec = new float[DIM];
        for (int i = 0; i < DIM; i++)
            vec[i] = (float)(rnd.NextDouble() * 2.0 - 1.0); // Werte in [-1, 1]
        return vec;
    }
}
