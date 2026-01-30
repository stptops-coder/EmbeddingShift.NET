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

Notes:
- Uses built-in demo assets under `samples/insurance/`.
- Persists embeddings/manifests under `data/`.
- Writes an evaluation run under `results/` (or `results/insurance/tenants/<tenant>/...` if `--tenant` is set).

## Artifact roots

Default:
- `data/` and `results/` under repo root

Override:
- `EMBEDDINGSHIFT_ROOT=<path>` → `<path>/data` and `<path>/results`

Tenant layout:
- Generic evaluation: `results/insurance/tenants/<tenant>/...`
- Mini-Insurance: `results/insurance/tenants/<tenant>/...`
