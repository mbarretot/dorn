using Dorn.Cli.Output;

namespace Dorn.Cli.Tests.Output;

/// <summary>
/// Captures writes for JSON assertions instead of <see cref="Spectre.Console.Testing.TestConsole"/>,
/// which only avoids width-wrapping because callers pin <c>Width(int.MaxValue)</c> — a setting
/// production code never applies.
/// </summary>
public sealed class RecordingCliOutputWriter : ICliOutputWriter
{
    public List<string> Lines { get; } = [];

    public void WriteLine(string value) => Lines.Add(value);
}
