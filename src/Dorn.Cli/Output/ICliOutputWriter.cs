namespace Dorn.Cli.Output;

/// <summary>
/// Writes raw text directly to the underlying stream, bypassing <see cref="Spectre.Console.IAnsiConsole"/>
/// so machine-readable output (e.g. JSON) is never width-wrapped or otherwise reformatted.
/// </summary>
public interface ICliOutputWriter
{
    void WriteLine(string value);
}
