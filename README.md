# EmbeddingShift.NET

A .NET 8 toolkit for embedding-space **shift strategies** with evaluation and simulation.  
Focus: reproducible workflows and inspectable artifacts (file-based).

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
- `--tenant <key>` writes generic evaluation runs under `results/tenants/<key>/...`
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
