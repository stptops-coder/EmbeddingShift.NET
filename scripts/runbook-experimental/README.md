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

For the canonical verification path, use:

- `scripts/runbook/README.md`
