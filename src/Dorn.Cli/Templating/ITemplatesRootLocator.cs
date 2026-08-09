using Dorn.Core.Templating;

namespace Dorn.Cli.Templating;

/// <summary>
/// Test seam over the static <see cref="Dorn.Core.Templating.TemplateLocator"/> (which reads
/// the process-global DORN_TEMPLATES_PATH). Mirrors <c>ISignalRegistration</c>.
/// </summary>
public interface ITemplatesRootLocator
{
    /// <summary>Returns the absolute templates root; throws when it cannot be resolved.</summary>
    string Resolve();
}

/// <summary>Forwards to the static <see cref="TemplateLocator"/>.</summary>
public sealed class TemplatesRootLocator : ITemplatesRootLocator
{
    public string Resolve() => TemplateLocator.ResolveTemplatesRoot();
}
