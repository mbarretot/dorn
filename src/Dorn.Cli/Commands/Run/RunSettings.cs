using Dorn.Cli.Commands.Shared;

namespace Dorn.Cli.Commands.Run;

/// <summary>
/// Settings for the <c>dorn run</c> command. Inherits <c>--project</c> from the
/// shared base; orchestrator is detected at runtime from file presence, not from flags.
/// </summary>
public sealed class RunSettings : ProjectCommandSettings { }
