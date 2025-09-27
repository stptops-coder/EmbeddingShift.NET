# RakeX – ConsoleEval How-To (Ingest → Persist → Evaluate)

This quick guide shows how to run the **ingest → persist → evaluate** loop from the **solution root**.

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

## Build

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

### 2) Ingest References
Parses text lines from the same folder and persists them as `<dataset>:refs`.

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- ingest-refs samples/demo DemoDataset
```

> You can also pass a **single file** instead of a folder (one entry per line).

---

## Evaluate

### A) Full dataset evaluation (console summary)
Evaluates stored `queries` against `refs` for the chosen dataset.

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- evaluate DemoDataset
```

### B) Ad-hoc single query (without saving)
Run an inline query text against stored refs:

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- evaluate DemoDataset --query "Example question about refunds"
```

### C) Evaluate a file of queries (one per line)
```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- evaluate-file DemoDataset samples/demo/demo.txt
```

> The console prints top matches and similarity scores.  
> Depending on configuration, results may also be exported to a file.

---

## Switching Backends

- **Simulator (default):** `EMBEDDING_BACKEND=sim`
- **OpenAI:** `EMBEDDING_BACKEND=openai` and set `OPENAI_API_KEY`

Change the env var and re-run; no code changes required.

---

## Data Location

By default, embeddings and artifacts are persisted under the console app’s data path (e.g., `./data/<dataset>/...`).  
If you customized storage (SQLite/JSONL/etc.), the console uses that configuration.

---

## Clean Up

To reset a demo run, remove the dataset folder under the data path (or use a `delete-dataset` command if present).

---

## Troubleshooting

- **Nothing printed?** Try a verbose flag (if supported) and ensure dataset names match exactly.  
- **Backend errors:** Check `EMBEDDING_BACKEND` and, for OpenAI, `OPENAI_API_KEY`.  
- **File not found:** Provide a folder *or* a file path; for folders, all `*.txt` are read (one entry per line).
