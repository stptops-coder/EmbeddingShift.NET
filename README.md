# EmbeddingShift.NET

A .NET 8 toolkit for embedding-space **shift strategies** with evaluation and simulation.  
Focus: reproducible workflows and inspectable artifacts (file-based).

## Concept in 60 seconds

A typical workflow is:

1. **Ingest** documents/queries into a dataset scope (file-based artifacts).
2. **Embed + evaluate** a **baseline** retrieval run.
3. **Train/apply a shift** (e.g., Pos/Neg, adaptive, etc.) and **re-evaluate**.
4. **Compare runs** using retrieval metrics (e.g., NDCG/MRR) to decide whether a shift is beneficial.

**Pos/Neg (intuition):** learn a direction from *positive vs. negative* examples and apply it as an embedding-space shift (no model retraining).

This targets the *retrieval layer* that is also used in RAG systems; it does not implement prompting/LLM orchestration.
For metric definitions and interpretation, see `Docs/CLI-Metrics.md`.

## Quickstart

Run commands from the repository root (the folder that contains `src/` and `scripts/`).

```powershell
dotnet build
dotnet test
dotnet run --project src/EmbeddingShift.ConsoleEval -- help
dotnet run --project src/EmbeddingShift.ConsoleEval -- run-smoke-demo
```

`run-smoke-demo` uses the built-in demo assets under `samples/insurance/` and runs:

1) ingest (persist embeddings + manifests to `data/`)  
2) dataset validation  
3) evaluation (writes a run report under `results/`)

## Runbooks (recommended)

If you want a *stable*, copy/pasteable sequence (and you prefer not to rely on private temp folders), use the runbook scripts:

- **PosNeg (deterministic, full sequence)**  
  `.\scripts\runbook\25-PosNeg-Deterministic-Full.ps1`  
  (generates dataset → trains PosNeg → runs baseline vs PosNeg → prints history/best/inspect)

- **Acceptance sweep (deterministic)**  
  `.\scripts\runbook\21-AcceptanceSweep-Deterministic.ps1`  
  (runs a grid of dataset sizes → compares/decides best run; promotion is optional via `-Promote`)

Notes:
- These scripts default to writing run roots under `results\_scratch\...` **inside the repo** (so you can keep everything in one place and avoid `%TEMP%`).  
  If you explicitly want `%TEMP%`, pass `-RootMode temp`.
- `dataset-status` / `dataset-validate` are **top-level CLI commands**, not `domain mini-insurance ...` subcommands.

## Smoke-all (full end-to-end demo)

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- --tenant insurer-a --backend=sim --sim-mode=deterministic smoke-all
```

This runs:
- `run-smoke-demo` (with reset)
- Mini-Insurance First/Delta pipeline (under `results/insurance/tenants/<tenant>/...`)
- PosNeg training (micro) and PosNeg run

## Artifact roots

Default (repo root):
- `data/` — persisted embeddings + ingest manifests (FileStore)
- `results/` — evaluation runs, comparisons, reports

Override both roots:
- `EMBEDDINGSHIFT_ROOT=<path>` → uses `<path>/data` and `<path>/results`

Tenant scoping:
- `--tenant <key>` writes generic evaluation runs under `results/insurance/tenants/<key>/...`
- Mini-Insurance writes under `results/insurance/tenants/<key>/...`

## Embedding backend

- Default: `--backend=sim`
- `--backend=openai` exists as a scaffold but is **not wired** in this repo state (throws `NotSupportedException`).

## Documentation

- Repo overview: [Docs/OVERVIEW_TOUR.md](Docs/OVERVIEW_TOUR.md)
- CLI onboarding (recommended): [Docs/CLI-Onboarding.md](Docs/CLI-Onboarding.md)
- CLI full reference: [Docs/CLI-Full-Guide.md](Docs/CLI-Full-Guide.md)
- CLI how-to (project-local): [src/EmbeddingShift.ConsoleEval/HowToRun.md](src/EmbeddingShift.ConsoleEval/HowToRun.md)
- Runbook: [scripts/RUNBOOK_FirstLight.md](scripts/RUNBOOK_FirstLight.md)
- Mini-Insurance reference:
  - [Docs/MiniInsuranceFirstDeltaLoop.md](Docs/MiniInsuranceFirstDeltaLoop.md)
  - [Docs/MiniInsuranceAdaptiveOverview.md](Docs/MiniInsuranceAdaptiveOverview.md)

## License

MIT — see [LICENSE](LICENSE).
