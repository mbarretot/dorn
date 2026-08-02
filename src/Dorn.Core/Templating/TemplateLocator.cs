namespace Dorn.Core.Templating;

/// <summary>
/// Resolves the filesystem root of Dorn's templates/ folder (the parent directory that
/// contains one subdirectory per template, e.g. templates/webapi).
/// </summary>
public static class TemplateLocator
{
    private const string EnvironmentVariableName = "DORN_TEMPLATES_PATH";
    private const string TemplatesFolderName = "templates";
    private const string TemplateConfigFolderName = ".template.config";

    /// <summary>Resolves templates from DORN_TEMPLATES_PATH or the packaged/repository directory layout.</summary>
    public static string ResolveTemplatesRoot()
    {
        var envOverride = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(envOverride))
        {
            if (!Directory.Exists(envOverride))
            {
                throw new DirectoryNotFoundException(
                    $"{EnvironmentVariableName} was set to '{envOverride}' but that directory does not exist."
                );
            }

            return Path.GetFullPath(envOverride);
        }

        if (TryFindTemplatesRootFromBaseDirectory(out var resolved))
        {
            return resolved;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the Dorn templates root. Set the "
                + $"{EnvironmentVariableName} environment variable to point at the 'templates' "
                + "directory, or run Dorn from an install layout that ships a 'templates' folder "
                + "alongside the tool."
        );
    }

    private static bool TryFindTemplatesRootFromBaseDirectory(out string templatesRoot)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, TemplatesFolderName);
            if (Directory.Exists(candidate) && ContainsTemplate(candidate))
            {
                templatesRoot = candidate;
                return true;
            }

            if (
                string.Equals(current.Name, TemplatesFolderName, StringComparison.OrdinalIgnoreCase)
                && ContainsTemplate(current.FullName)
            )
            {
                templatesRoot = current.FullName;
                return true;
            }

            current = current.Parent;
        }

        templatesRoot = string.Empty;
        return false;
    }

    private static bool ContainsTemplate(string directory)
    {
        return Directory
            .EnumerateDirectories(directory)
            .Any(sub => Directory.Exists(Path.Combine(sub, TemplateConfigFolderName)));
    }
}
