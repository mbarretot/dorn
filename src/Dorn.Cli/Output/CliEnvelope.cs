namespace Dorn.Cli.Output;

public sealed record CliEnvelope<TData>(
    int SchemaVersion,
    string Command,
    bool Success,
    int ExitCode,
    TData Data
);
