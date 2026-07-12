using Spectre.Console.Cli;

namespace Dorn.Cli.Infrastructure;

public sealed class TypeResolver(IServiceProvider provider) : ITypeResolver, IDisposable
{
    private readonly IServiceProvider _provider =
        provider ?? throw new ArgumentNullException(nameof(provider));

    public object? Resolve(Type? type)
    {
        return type is null ? null : _provider.GetService(type);
    }

    public void Dispose()
    {
        if (_provider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
