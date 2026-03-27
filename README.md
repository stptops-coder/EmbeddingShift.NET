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

If you want one stable, public-facing verification flow, start with the canonical runbook entrypoint:

- **Standard gate (recommended)**  
  `scripts/runbook/README.md`  
  Canonical sequence: `00-Prep` → `10-Build` → `30-Tests` → `21-AcceptanceSweep-Deterministic`

Useful follow-up entry points:
- **Acceptance sweep (deterministic)**  
  `.\scripts\runbook\21-AcceptanceSweep-Deterministic.ps1`  
  (runs a grid of dataset sizes → compares/decides best run; promotion is optional via `-Promote`)
- **Larger PosNeg experiment (optional / experimental)**  
  `.\scripts\runbook-experimental\25-PosNeg-Deterministic-Full.ps1`

Notes:
- These scripts default to writing run roots under `results\_scratch\...` **inside the repo** (so you can keep everything in one place and avoid `%TEMP%`).  
  If you explicitly want `%TEMP%`, pass `-RootMode temp`.
- `dataset-status` / `dataset-validate` are **top-level CLI commands**, not `domain mini-insurance ...` subcommands.

## Public repo status

Implemented in this repo today:
- deterministic simulation backend (`--backend=sim`)
- file-based ingest/eval/run artifacts
- reproducible run comparison / decide / promote flow
- a partially general retrieval-evaluation core (persisted runs, per-query eval artifacts, compare/decide/promote)
- Mini-Insurance as the main reference/demo domain

Visible as demo or partial packaging:
- `run-smoke-demo` and the Mini-Insurance pipeline
- segment-based analysis commands that consume externally produced JSON decisions

Scaffold / not fully wired in this public repo state:
- `--backend=openai` exists as a scaffold but is not wired end-to-end
- adaptive/generator demos are not part of the standard ingest → eval → promote path
- routing is not yet packaged in the same externalized JSON form as the segment experiments

Interpretation:
- the retrieval-evaluation core is already broader than a single shift workflow
- some packaging still remains Mini-Insurance / shift-specific (for example parts of segment compare and artifact naming)

## Smoke-all (full end-to-end demo)

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- --tenant insurer-a --backend=sim --sim-mode=deterministic smoke-all
```

This runs a broader demo chain:
- `run-smoke-demo` (with reset)
- Mini-Insurance First/Delta pipeline (under `results/insurance/tenants/<tenant>/...`)
- PosNeg training (micro) and PosNeg run

This is useful for demos, but the default public verification path remains the standard runbook gate above.

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

- Runbook verification gate (canonical): [scripts/runbook/README.md](scripts/runbook/README.md)
- CLI onboarding (recommended quickstart): [Docs/CLI-Onboarding.md](Docs/CLI-Onboarding.md)
- Repo overview: [Docs/OVERVIEW_TOUR.md](Docs/OVERVIEW_TOUR.md)
- CLI full reference: [Docs/CLI-Full-Guide.md](Docs/CLI-Full-Guide.md)
- CLI how-to (project-local): [src/EmbeddingShift.ConsoleEval/HowToRun.md](src/EmbeddingShift.ConsoleEval/HowToRun.md)
- FirstLight runbook (legacy/specialized): [scripts/RUNBOOK_FirstLight.md](scripts/RUNBOOK_FirstLight.md)
- Mini-Insurance reference:
  - [Docs/MiniInsuranceFirstDeltaLoop.md](Docs/MiniInsuranceFirstDeltaLoop.md)
  - [Docs/MiniInsuranceAdaptiveOverview.md](Docs/MiniInsuranceAdaptiveOverview.md)

## License

MIT — see [LICENSE](LICENSE).
