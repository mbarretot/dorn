namespace Dorn.Cli.Execution;

/// <summary>
/// Describes a process to be run by <see cref="IProcessRunner"/>.
/// </summary>
/// <param name="FileName">The executable name or path.</param>
/// <param name="Arguments">The command-line arguments passed to the executable.</param>
/// <param name="WorkingDirectory">
/// The working directory for the process. Defaults to the current directory if not specified.
/// </param>
public record ProcessSpec(
    string FileName,
    IReadOnlyList<string> Arguments,
    string? WorkingDirectory = null
)
{
    /// <summary>Initializes a new instance of the <see cref="ProcessSpec"/> record.</summary>
    public ProcessSpec(string fileName, params string[] arguments)
        : this(fileName, (IReadOnlyList<string>)arguments, Directory.GetCurrentDirectory()) { }
}
