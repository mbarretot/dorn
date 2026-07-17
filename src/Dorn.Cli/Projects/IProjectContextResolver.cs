namespace Dorn.Cli.Projects;

/// <summary>
/// Resolves a directory on disk into a <see cref="ProjectContext"/> describing
/// the generated Clean Architecture project found there.
/// </summary>
public interface IProjectContextResolver
{
    /// <summary>
    /// Resolves the project context for the given root directory.
    /// </summary>
    ProjectContext Resolve(string rootPath);
}
