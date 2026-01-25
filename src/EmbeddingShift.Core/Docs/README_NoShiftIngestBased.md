﻿# Baseline Shift (NoShiftIngestBased)

This configuration represents the **Baseline mode ("Pretrained Model as is")**.

It uses the shift class `NoShiftIngestBased`, which applies **no transformation** to the embedding vector.
This baseline allows you to evaluate retrieval and similarity performance in the original embedding space.

---

### Concept

- **Goal:** Measure retrieval quality in the original embedding space.
- **Shift Type:** Identity transformation (input == output)
- **Use Case:** Control run for comparison with Additive, Multiplicative, or Adaptive shifts.
- **Evaluation:** Enables metrics such as cosine similarity, nDCG, and MRR to be computed
  under pure Method-A conditions.

---

### Example (ConsoleEval)

`ash
dotnet run --project EmbeddingShift.ConsoleEval -- --shift NoShiftIngestBased --space Diagnostics
`$nl
Output shows baseline scores that serve as the reference for later shifted runs.

---

### Summary

| Layer | Role | Description |
|-------|------|-------------|
| Embedding Provider | Simulated (default); OpenAI scaffold (not wired) | Produces original vectors |
| Shift | NoShiftIngestBased | Applies no transformation |
| Vector Store | FileStore | Persists embeddings and metrics |
| Evaluation | Cosine, MRR, nDCG | Quantifies baseline performance |

This represents the **pure Method A setup**  using pretrained embeddings "as is".
