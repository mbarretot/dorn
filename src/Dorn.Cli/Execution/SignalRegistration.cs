using System.Runtime.InteropServices;

namespace Dorn.Cli.Execution;

/// <summary>Forwards to <see cref="PosixSignalRegistration"/>.</summary>
public sealed class SignalRegistration : ISignalRegistration
{
    public IDisposable Register(PosixSignal signal, Action<PosixSignalContext> handler)
    {
        return PosixSignalRegistration.Create(
            (PosixSignal)(int)signal,
            ctx =>
            {
                handler(new PosixSignalContext((PosixSignal)(int)ctx.Signal));
            }
        );
    }
}
