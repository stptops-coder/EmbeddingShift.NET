# Overview Tour

This document provides a compact orientation of the repository and the default runtime layout.

## 1) What this repo is

EmbeddingShift.NET focuses on **embedding-space shift strategies** and **measurable evaluation**.
The emphasis is on:
- reproducible runs
- file-based artifacts (easy to inspect, diff, archive)
- a CLI that can drive end-to-end workflows

## 2) Key folders

- `src/` — all projects (Core, Workflows, CLI, Tests, Simulation, etc.)
- `samples/` — demo and Mini-Insurance sample assets
- `scripts/` — runbooks and portable scripts
- `data/` — persisted embeddings + ingest manifests (default location)
- `results/` — run outputs, comparisons, reports (default location)

## 3) Default artifact layout

By default, runs write into repo-local folders:
- `data/` for persisted embeddings/manifests
- `results/` for reports and run artifacts

Override both roots:
- `EMBEDDINGSHIFT_ROOT=<path>` → uses `<path>/data` and `<path>/results`

Optional tenant isolation:
- `EMBEDDINGSHIFT_TENANT=<key>` → tenant-aware layout under `results/`

## 4) Fastest way to see it working

From the repo root:

```bash
dotnet build
dotnet test
dotnet run --project src/EmbeddingShift.ConsoleEval -- run-smoke-demo
```

## 5) Next docs to read

- CLI quick guide: `src/EmbeddingShift.ConsoleEval/HowToRun.md`
- Runbook (portable scripts): `scripts/RUNBOOK_FirstLight.md`
- Mini-Insurance: `Docs/MiniInsuranceFirstDeltaLoop.md`
