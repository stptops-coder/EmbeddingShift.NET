# RakeX – ConsoleEval How-To (Ingest → Persist → Eval)

This quick guide shows how to run the **ingest → persist → eval** loop from the **solution root**.

> Paths assume repo root: `C:\pg\RakeX`  
> Project: `src/EmbeddingShift.ConsoleEval`

---

## Prerequisites

- .NET 8 SDK installed (`dotnet --info`)
- (Optional) OpenAI API for real embeddings  
  - Set `OPENAI_API_KEY` in your environment if you want to use real embeddings.
  - Otherwise, the **simulator** backend is fine for local runs.

### Environment (PowerShell)
```powershell
# optional – only if using the OpenAI backend
$env:OPENAI_API_KEY = "<your-key>"

# choose backend: "sim" (default) or "openai"
$env:EMBEDDING_BACKEND = "sim"
```

---

## Build (once)

```powershell
cd C:\pg\RakeX
dotnet restore
dotnet build -c Release
```

---

## Quick Start (DemoDataset)

### 1) Ingest Queries
Parses text lines from `samples/demo` and persists them as embeddings under `<dataset>:queries`.

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- ingest-queries samples/demo DemoDataset
```
Expected console message:  
`Ingest (queries) finished.`

### 2) Ingest References
Parses text lines from the same folder and persists them as `<dataset>:refs`.

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- ingest-refs samples/demo DemoDataset
```
Expected console message:  
`Ingest (refs) finished.`

> You can also pass a **single file** instead of a folder (one entry per line).

---

## 3) Eval (persisted embeddings)

Evaluates stored `queries` against `refs` for the chosen dataset and writes a result bundle under `./results/<timestamp>_<run-id>`.

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- eval DemoDataset
```

Typical console output (example):
```
Found 3 persisted embeddings for 'DemoDataset:queries' under: C:\pg\RakeX\src\EmbeddingShift.ConsoleEvalin\Debug
et8.0\data\embeddings
Found 3 persisted embeddings for 'DemoDataset:refs' under: C:\pg\RakeX\src\EmbeddingShift.ConsoleEvalin\Debug
et8.0\data\embeddings
Eval mode: persisted embeddings (dataset 'DemoDataset'): 3 queries vs 3 refs.
[RUN START] <run-guid> | evaluation | DemoDataset
[RUN <run-guid>] CosineSimilarityEvaluator = 0.3068
[RUN <run-guid>] MarginEvaluator = 0.8882
[RUN <run-guid>] NdcgEvaluator = 0.6667
[RUN <run-guid>] MrrEvaluator = 0.5556
[RUN END] <run-guid> | Results at ./results/2025xxxx_xxxxxx-xxxxxxxx
```

You can open the folder printed after `Results at ...` to inspect exported artifacts (e.g., JSON or CSV, depending on configuration).

---

## Data Locations

- **Embeddings (persisted):**  
  `src/EmbeddingShift.ConsoleEval/bin/<Config>/net8.0/data/embeddings`
- **Results (eval runs):**  
  `src/EmbeddingShift.ConsoleEval/bin/<Config>/net8.0/results` (also echoed as `./results/...`)

> `<Config>` is usually `Debug` unless you build with `-c Release`.

---

## Switching Backends

- **Simulator (default):** `EMBEDDING_BACKEND=sim`
- **OpenAI:** `EMBEDDING_BACKEND=openai` and set `OPENAI_API_KEY`

Change the env var and re-run; no code changes required.

---

## Clean Up

To reset a demo run, remove the dataset folder under the data path (or use a `delete-dataset` command if present).

---

## Troubleshooting

- **Nothing printed?** Ensure dataset names match exactly and that you ran both ingest steps first.  
- **Backend errors:** Check `EMBEDDING_BACKEND` and, for OpenAI, `OPENAI_API_KEY`.  
- **File not found:** Provide a folder *or* a file path; for folders, all `*.txt` are read (one entry per line).
