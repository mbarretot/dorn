using System.Runtime.InteropServices;

namespace Dorn.Cli.Execution;

/// <summary>
/// Abstraction over <see cref="PosixSignalRegistration"/> for the Compose Ctrl+C
/// teardown path. Production implementation forwards SIGINT/SIGTERM to a handler;
/// tests substitute a no-op or signal-emitting stub.
/// </summary>
public interface ISignalRegistration
{
    /// <summary>
    /// Subscribes <paramref name="handler"/> to <paramref name="signal"/> until disposed.
    /// </summary>
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
