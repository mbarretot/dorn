using Dorn.Cli.Commands.Shared;

namespace Dorn.Cli.Commands.Coverage;

/// <summary>
/// Settings for the <c>dorn coverage</c> command. Inherits <c>--project</c> from the
/// shared base; threshold is fixed (not configurable).
/// </summary>
public sealed class CoverageSettings : ProjectCommandSettings { }
