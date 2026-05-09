namespace EmbeddingShift.Agentic.Console;

public sealed record AgenticConsoleGlobalOptions(
    string ReasoningBackend = "sim");

public sealed record AgenticConsoleParsedArgs(
    AgenticConsoleGlobalOptions Options,
    string[] CommandArgs);
