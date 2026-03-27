# ConsoleEval — How to run

Run commands from the repository root:

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- <command> [args]
```

Recommended for repeated runs:

```powershell
dotnet build -c Release
dotnet run --no-build -c Release --project src/EmbeddingShift.ConsoleEval -- <command> [args]
```

## Scope of this note

This file is a **project-local CLI reminder**.

For the stable PowerShell verification gate, use:
- `scripts/runbook/README.md`

For the broader CLI reference, use:
- `Docs/CLI-Full-Guide.md`

Public-facing rule of thumb:
- `run-smoke-demo` = fastest demo
- `smoke-all` = broader public demo chain
- `scripts/runbook/README.md` = standard verification path
- OpenAI/adaptive notes in this repo are scaffold/demo unless stated otherwise

## Discover commands

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- help
```

## Global flags (may appear anywhere; removed before dispatch)

Common defaults (sim):
- `--backend=sim`
- `--sim-mode=deterministic` (or `noisy`)
- optional tenant scoping: `--tenant insurer-a`

Example:

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- --tenant insurer-a --backend=sim --sim-mode=deterministic smoke-all
```

## Quickstart

Fastest working run (no paths required):

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- run-smoke-demo
```

Broader public demo chain:

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- --tenant insurer-a --backend=sim --sim-mode=deterministic smoke-all
```

Notes:
- `run-smoke-demo` uses built-in demo assets under `samples/insurance/`.
- `smoke-all` adds the Mini-Insurance pipeline and PosNeg micro flow on top of the smoke demo.
- The standard verification path still remains `scripts/runbook/README.md`.
- Both write under `data/` / `results/` (tenant-aware when `--tenant` is set).

## Artifact roots

Default:
- `data/` and `results/` under repo root

Override:
- `EMBEDDINGSHIFT_ROOT=<path>` → `<path>/data` and `<path>/results`

Tenant layout:
- Generic evaluation: `results/insurance/tenants/<tenant>/...`
- Mini-Insurance: `results/insurance/tenants/<tenant>/...`
