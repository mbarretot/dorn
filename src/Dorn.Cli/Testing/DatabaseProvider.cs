namespace Dorn.Cli.Testing;

/// <summary>
/// Database provider used by the target generated project, used by
/// <see cref="DotnetTestRunner"/> to decide whether to emit the Docker preflight warning.
/// </summary>
public enum DatabaseProvider
{
    Sqlite,
    SqlServer,
}
