# Experimental runbooks

These scripts are optional advanced workflows. They are not part of the standard verification gate.

Recommended advanced workflows:

- `25-PosNeg-Deterministic-Full.ps1`  
  Smaller deterministic Pos/Neg workflow.

- `35-PosNegBigRunAll-Deterministic.ps1`  
  Larger deterministic Pos/Neg run for one seed/size setup.

- `36-PosNegBigRunMatrix-Deterministic.ps1`  
  Matrix runner across seeds and dataset sizes.

- `37-PosNegBigSummarize.ps1`  
  Collects decision artifacts into CSV/Markdown summaries.


## Quick examples

Run from the repository root.

### Compact advanced PosNeg sanity run

```powershell
.\scripts\runbook-experimental\35-PosNegBigRunAll-Deterministic.ps1 `
  -Policies 80 `
  -Queries 160 `
  -Stages 3 `
  -StageIndices 1,2 `
  -Seed 1337 `
  -TrainMode production `
  -HardNegTopK 5 `
  -PosNegScales 1.0 `
  -SkipPrep `
  -SkipBuild `
  -SkipTests
```

### Small matrix run

```powershell
.\scripts\runbook-experimental\36-PosNegBigRunMatrix-Deterministic.ps1 `
  -CleanScenario `
  -Seeds 1337,2026 `
  -SizeMatrix 100x200 `
  -Stages 3 `
  -StageIndices 1,2 `
  -TrainMode production `
  -HardNegTopK 5 `
  -PosNegScales 1.0 `
  -SkipTests
```

### Summarize matrix decisions

```powershell
.\scripts\runbook-experimental\37-PosNegBigSummarize.ps1 `
  -Scenario EmbeddingShift.PosNegBigMatrix `
  -Tenant insurer-a `
  -Metric ndcg@3
```

For the canonical verification path, use:

- `scripts/runbook/README.md`
