# Python cosine spike

Optional Python/NumPy cosine scoring in the Mini-Insurance and PosNeg workflows.
C# handles embeddings, shifts, ranking and result files.

Run from the repository root with .NET 8 and Python installed.

Setup creates `python/.venv` and installs NumPy:

```powershell
.\python\Setup.ps1
dotnet build EmbeddingShift.sln
```

Run with C# (the default):

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- --cosine=csharp domain mini-insurance pipeline
```

Run with Python:

```powershell
dotnet run --project src/EmbeddingShift.ConsoleEval -- --cosine=python domain mini-insurance pipeline
```
