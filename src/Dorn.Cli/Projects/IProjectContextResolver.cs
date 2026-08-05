namespace Dorn.Cli.Projects;

public interface IProjectContextResolver
{
    ProjectContext Resolve(string rootPath);
}
