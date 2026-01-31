# Overview Tour

This document provides a compact orientation of the repository and the default runtime layout.

## 1) What this repo is

EmbeddingShift.NET focuses on **embedding-space shift strategies** and **measurable evaluation**.
The emphasis is on:
- reproducible runs
- file-based artifacts (easy to inspect, diff, archive)
- a CLI that can drive end-to-end workflows

## 2) Workflow overview (one page)

The repo is organized around **reproducible runs** that produce inspectable artifacts:

- **Baseline run**: ingest → embed → retrieve → compute metrics.
- **Shifted run**: use the same dataset/scope, apply one or more **embedding-space shifts**, then re-run retrieval + metrics.
- **Comparison**: decide “better/worse” by **metric deltas** (e.g., NDCG@K / MRR@K) and simple acceptance gates.

### Pos/Neg shift (intuition)

Pos/Neg learns a directional shift from *positive vs. negative* examples and applies it in embedding space before retrieval (no base-model retraining).

Most CLI commands are just different ways to produce runs (baseline, training, inspection) while keeping the artifact layout consistent.

## 3) Key folders

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
