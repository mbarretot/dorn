namespace CleanArchGrpcService.Architecture.Tests;

/// <summary>
/// Enforces the layering rules from the grpc template README as executable checks (ArchUnitNET
/// — see ADR 0013), since nothing stops an errant `using` from compiling otherwise. Ported from
/// <c>templates/webapi</c>'s <c>LayeringTests</c> with <c>WebApi</c> → <c>Grpc</c>; adds one new
/// rule (<c>Application_ShouldNot_DependOnGrpcLibraries</c>) because gRPC's presentation adapter
/// is a new external coupling that the Application layer must not absorb.
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
    private static readonly IObjectProvider<IType> Grpc = InNamespace(
        "CleanArchGrpcService.Grpc"
    );

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
                    .ResideInNamespaceMatching(@"^CleanArchGrpcService\.(Infrastructure|Grpc)(\.|$)")
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
        // Distinct from Application_ShouldNot_DependOnInfrastructureOrGrpc: this guards against
        // leaking any gRPC-flavoured contract (Grpc.Core, Grpc.AspNetCore, Google.Protobuf,
        // Grpc.Net.Client) into the Application layer's runtime references. Even an indirect
        // transitively-pulled reference would surface here because LoadAssembliesIncludingDependencies
        // walks the full closure.
        Types()
            .That()
            .Are(Application)
            .Should()
            .NotDependOnAny(
                Types().That().ResideInNamespaceMatching(@"^(Grpc\.|Google\.Protobuf)")
            )
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