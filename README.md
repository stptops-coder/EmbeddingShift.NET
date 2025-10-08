# EmbeddingShift.NET

A .NET 8 toolkit for embedding-space **shift strategies** with evaluation and simulation.
Focus: transparent, testable workflows for domain-heavy contexts (e.g., diagnostics, medical, or technical knowledge systems).

## Overview
- **Purpose:** Apply and evaluate shifts (e.g., `NoShift.IngestBased`) to improve retrieval quality.
- **Motivation:** Domains evolve; shifts enable adaptation without retraining a foundation model.
- **Workflow:** Ingest → Persist → Evaluate (MRR/nDCG) → (Optional) Adaptive selection.

## Quickstart
~~~bash
dotnet build
dotnet run --project src/EmbeddingShift.ConsoleEval -- --demo samples/demo/demo.txt --shift NoShift.IngestBased
dotnet test
~~~

## Project structure
- **Abstractions** – interfaces (`IShift`, `IShiftEvaluator`, `IVectorStore` …)
- **Core** – base shifts/evaluators (`NoShiftIngestBased`, runners)
- **Adaptive** – `AdaptiveShiftController`, selection & evaluation service
- **Workflows** – scripted flows (ingest → persist → eval)
- **Simulation** – offline runs without external APIs
- **Console / ConsoleEval** – CLI & demo
- **Tests** – unit tests for shifts/evaluators
- **Docs** – architecture and usage notes
- **scripts** – PowerShell helpers (optional)

## Domain alignment
- Reproducible runs and clear interfaces suited for complex, regulated domains.
- Modular and easy to extend with new shifts or evaluation metrics.
- Includes `NoShift.IngestBased` as identity baseline; infrastructure for adaptive selection.

## Status
- ✅ Public demo paths ready
- ✅ Evaluators and tests included
- ⏩ Next step: B-Light Inside (post-initial release)

## Further reading
See [`Docs/OVERVIEW_TOUR.md`](Docs/OVERVIEW_TOUR.md) for a short technical walkthrough.

## License
This project is licensed under the MIT License — see [LICENSE](LICENSE) for details.
