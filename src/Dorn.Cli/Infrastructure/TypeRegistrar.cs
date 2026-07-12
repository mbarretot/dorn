using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace Dorn.Cli.Infrastructure;

/// <summary>
/// Bridges Microsoft.Extensions.DependencyInjection into Spectre.Console.Cli's own
/// ITypeRegistrar/ITypeResolver abstraction. Standard pattern documented by Spectre.Console.Cli.
/// </summary>
public sealed class TypeRegistrar(IServiceCollection services) : ITypeRegistrar
{
    private readonly IServiceCollection _services = services;

    public ITypeResolver Build()
    {
        return new TypeResolver(_services.BuildServiceProvider());
    }

    public void Register(Type service, Type implementation)
    {
        _services.AddSingleton(service, implementation);
    }

    public void RegisterInstance(Type service, object implementation)
    {
        _services.AddSingleton(service, implementation);
    }

    public void RegisterLazy(Type service, Func<object> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _services.AddSingleton(service, _ => factory());
    }
}
