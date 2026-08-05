using System.Runtime.InteropServices;

namespace Dorn.Cli.Execution;

/// <summary>
/// Abstracts <see cref="PosixSignalRegistration"/> for Compose Ctrl+C teardown; production forwards signals and tests use stubs.
/// </summary>
public interface ISignalRegistration
{
    IDisposable Register(PosixSignal signal, Action<PosixSignalContext> handler);
}

/// <summary>
/// Wrapped <see cref="PosixSignalContext"/> for testability. Mirrors the .NET type's
/// payload but keeps tests independent of <c>System.Runtime.InteropServices</c>.
/// </summary>
public sealed record PosixSignalContext(PosixSignal Signal)
{
    /// <summary>Indicates the signal was handled; the process will not terminate.</summary>
    public bool Handled { get; set; }
}
