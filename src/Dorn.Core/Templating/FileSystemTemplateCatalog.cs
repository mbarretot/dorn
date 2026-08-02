using Dorn.Abstractions.Templates;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Utils;

namespace Dorn.Core.Templating;

/// <summary>
/// Scans Dorn's source templates directly; the singleton keeps the lazy-read scan mount point open.
/// </summary>
public sealed class FileSystemTemplateCatalog : ITemplateCatalog, IDisposable
{
    private readonly IEngineEnvironmentSettings _environmentSettings;
    private readonly SemaphoreSlim _scanLock = new(1, 1);
    private ScanResult? _scanResult;
    private IReadOnlyDictionary<
        string,
        (ITemplateInfo Info, TemplateDescriptor Descriptor)
    >? _templatesByShortName;

    public FileSystemTemplateCatalog(IEngineEnvironmentSettings environmentSettings)
    {
        _environmentSettings = environmentSettings;
    }

    public async Task<IReadOnlyList<TemplateDescriptor>> GetAvailableTemplatesAsync(
        CancellationToken ct = default
    )
    {
        var templates = await EnsureScannedAsync(ct).ConfigureAwait(false);
        return templates.Values.Select(t => t.Descriptor).ToList();
    }

    public async Task<TemplateDescriptor?> FindByShortNameAsync(
        string shortName,
        CancellationToken ct = default
    )
    {
        var templates = await EnsureScannedAsync(ct).ConfigureAwait(false);
        return templates.TryGetValue(shortName, out var entry) ? entry.Descriptor : null;
    }

    /// <summary>Returns raw template metadata needed by <see cref="TemplateCreator.InstantiateAsync"/>.</summary>
    public async Task<ITemplateInfo?> FindTemplateInfoByShortNameAsync(
        string shortName,
        CancellationToken ct = default
    )
    {
        var templates = await EnsureScannedAsync(ct).ConfigureAwait(false);
        return templates.TryGetValue(shortName, out var entry) ? entry.Info : null;
    }

    private async Task<
        IReadOnlyDictionary<string, (ITemplateInfo Info, TemplateDescriptor Descriptor)>
    > EnsureScannedAsync(CancellationToken ct)
    {
        if (_templatesByShortName is not null)
        {
            return _templatesByShortName;
        }

        await _scanLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_templatesByShortName is not null)
            {
                return _templatesByShortName;
            }

            var templatesRoot = TemplateLocator.ResolveTemplatesRoot();
            var scanner = new Scanner(_environmentSettings);
            _scanResult = await scanner
                .ScanAsync(templatesRoot, cancellationToken: ct)
                .ConfigureAwait(false);

            var map = new Dictionary<string, (ITemplateInfo, TemplateDescriptor)>(
                StringComparer.OrdinalIgnoreCase
            );
            foreach (var scanTemplateInfo in _scanResult.Templates)
            {
                var info = IScanTemplateInfoExtensions.ToITemplateInfo(scanTemplateInfo);

                // ShortName is obsolete in favor of ShortNameList (a template can have
                // multiple aliases); Dorn's own contracts only need a single short name,
                // so take the first entry and index every alias to the same descriptor.
                var primaryShortName = info.ShortNameList.FirstOrDefault() ?? info.Identity;
                var descriptor = new TemplateDescriptor(
                    Identity: info.Identity,
                    ShortName: primaryShortName,
                    Name: info.Name,
                    Description: info.Description,
                    Classifications: info.Classifications,
                    SourcePath: info.MountPointUri
                );

                foreach (var shortName in info.ShortNameList)
                {
                    map[shortName] = (info, descriptor);
                }
            }

            _templatesByShortName = map;
            return _templatesByShortName;
        }
        finally
        {
            _scanLock.Release();
        }
    }

    public void Dispose()
    {
        _scanResult?.Dispose();
        _scanLock.Dispose();
    }
}
