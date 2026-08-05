namespace CleanArchGrpcService.Architecture.Tests;

/// <summary>
/// Enforces the layering rules from the README as executable checks (ArchUnitNET, ADR 0013) —
/// nothing else stops an errant `using`. Adds <c>Application_ShouldNot_DependOnGrpcLibraries</c>
/// since gRPC is a new external coupling the Application layer must not absorb.
/// </summary>
public sealed class LayeringTests
{
    private static readonly ArchitectureModel Architecture = new ArchLoader()
        .LoadAssembliesIncludingDependencies(
            typeof(TodoItem).Assembly,
            typeof(CreateTodoItemCommand).Assembly,
            typeof(ApplicationDbContext).Assembly,
            typeof(Program).Assembly
        )
        .Build();

    private static IObjectProvider<IType> InNamespace(string root) =>
        Types().That().ResideInNamespaceMatching($@"^{Regex.Escape(root)}(\.|$)");

    private static readonly IObjectProvider<IType> Domain = InNamespace(
        "CleanArchGrpcService.Domain"
    );
    private static readonly IObjectProvider<IType> Application = InNamespace(
        "CleanArchGrpcService.Application"
    );
    private static readonly IObjectProvider<IType> Grpc = InNamespace("CleanArchGrpcService.Grpc");

    [Fact]
    public void Domain_ShouldNot_DependOnApplicationInfrastructureOrGrpc()
    {
        Types()
            .That()
            .Are(Domain)
            .Should()
            .NotDependOnAny(
                Types()
                    .That()
                    .ResideInNamespaceMatching(
                        @"^CleanArchGrpcService\.(Application|Infrastructure|Grpc)(\.|$)"
                    )
            )
            .Check(Architecture);
    }

    [Fact]
    public void Domain_ShouldNot_DependOnEntityFrameworkCore()
    {
        Types()
            .That()
            .Are(Domain)
            .Should()
            .NotDependOnAny(
                Types().That().ResideInNamespaceMatching(@"^Microsoft\.EntityFrameworkCore")
            )
            .Check(Architecture);
    }

    [Fact]
    public void Application_ShouldNot_DependOnInfrastructureOrGrpc()
    {
        Types()
            .That()
            .Are(Application)
            .Should()
            .NotDependOnAny(
                Types()
                    .That()
                    .ResideInNamespaceMatching(
                        @"^CleanArchGrpcService\.(Infrastructure|Grpc)(\.|$)"
                    )
            )
            .Check(Architecture);
    }

    [Fact]
    public void Infrastructure_ShouldNot_DependOnGrpc()
    {
        Types()
            .That()
            .ResideInNamespaceMatching(@"^CleanArchGrpcService\.Infrastructure(\.|$)")
            .Should()
            .NotDependOnAny(Types().That().Are(Grpc))
            .Check(Architecture);
    }

    [Fact]
    public void Application_ShouldNot_DependOnGrpcLibraries()
    {
        // Distinct from Application_ShouldNot_DependOnInfrastructureOrGrpc: catches transitive
        // gRPC references too (Grpc.Core/AspNetCore/Protobuf/Net.Client), since
        // LoadAssembliesIncludingDependencies walks the full closure.
        Types()
            .That()
            .Are(Application)
            .Should()
            .NotDependOnAny(Types().That().ResideInNamespaceMatching(@"^(Grpc\.|Google\.Protobuf)"))
            .Check(Architecture);
    }

    [Fact]
    public void RequestHandlers_Should_ResideInApplicationAssembly()
    {
        // ArchUnitNET's fluent predicates don't reliably target open generic interfaces, so
        // this one rule uses plain reflection instead — mirrors the webapi precedent.
        var handlerTypes = new[]
        {
            typeof(TodoItem).Assembly,
            typeof(CreateTodoItemCommand).Assembly,
            typeof(ApplicationDbContext).Assembly,
            typeof(Program).Assembly,
        }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type =>
                type.GetInterfaces()
                    .Any(i =>
                        i.IsGenericType
                        && i.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)
                    )
            )
            .ToList();

        Assert.NotEmpty(handlerTypes);
        Assert.All(
            handlerTypes,
            handlerType =>
                Assert.StartsWith(
                    "CleanArchGrpcService.Application",
                    handlerType.Namespace,
                    StringComparison.Ordinal
                )
        );
    }
}
