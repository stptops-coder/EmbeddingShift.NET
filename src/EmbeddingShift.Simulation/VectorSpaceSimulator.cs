using EmbeddingShift.Core.Utils;
namespace EmbeddingShift.Simulation;
public static class VectorSpaceSimulator {
  public static float[] Normalize(float[] v){ var len = Math.Sqrt(v.Sum(x => x*(double)x)); if(len==0) return v; return v.Select(x => (float)(x/len)).ToArray(); }
}


