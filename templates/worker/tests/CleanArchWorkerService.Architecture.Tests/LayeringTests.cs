namespace CleanArchWorkerService.Architecture.Tests;

/// <summary>
/// Enforces the layering rules from the README as executable checks (ArchUnitNET, ADR 0012).
/// Adds <c>Application_ShouldNot_DependOnHostingLibraries</c> since
/// <c>Microsoft.Extensions.Hosting</c> is a new external coupling the Application layer must not absorb.
/// </summary>
public sealed class LayeringTests
{
    private static readonly ArchitectureModel Architecture = new ArchLoader()
        .LoadAssembliesIncludingDependencies(
            typeof(TodoItem).Assembly,
            typeof(ProcessPendingTodoItemsCommand).Assembly,
            typeof(ApplicationDbContext).Assembly,
            typeof(Program).Assembly
        )
        .Build();

    private static IObjectProvider<IType> InNamespace(string root) =>
        Types().That().ResideInNamespaceMatching($@"^{Regex.Escape(root)}(\.|$)");

    private static readonly IObjectProvider<IType> Domain = InNamespace(
        "CleanArchWorkerService.Domain"
    );
    private static readonly IObjectProvider<IType> Application = InNamespace(
        "CleanArchWorkerService.Application"
    );
    private static readonly IObjectProvider<IType> Worker = InNamespace(
        "CleanArchWorkerService.Worker"
    );

    [Fact]
    public void Domain_ShouldNot_DependOnApplicationInfrastructureOrWorker()
    {
        Types()
            .That()
            .Are(Domain)
            .Should()
            .NotDependOnAny(
                Types()
                    .That()
                    .ResideInNamespaceMatching(
                        @"^CleanArchWorkerService\.(Application|Infrastructure|Worker)(\.|$)"
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
    public void Application_ShouldNot_DependOnInfrastructureOrWorker()
    {
        Types()
            .That()
            .Are(Application)
            .Should()
            .NotDependOnAny(
                Types()
                    .That()
                    .ResideInNamespaceMatching(
                        @"^CleanArchWorkerService\.(Infrastructure|Worker)(\.|$)"
                    )
            )
            .Check(Architecture);
    }

    [Fact]
    public void Infrastructure_ShouldNot_DependOnWorker()
    {
        Types()
            .That()
            .ResideInNamespaceMatching(@"^CleanArchWorkerService\.Infrastructure(\.|$)")
            .Should()
            .NotDependOnAny(Types().That().Are(Worker))
            .Check(Architecture);
    }

    [Fact]
    public void Application_ShouldNot_DependOnHostingLibraries()
    {
        Types()
            .That()
            .Are(Application)
            .Should()
            .NotDependOnAny(
                Types().That().ResideInNamespaceMatching(@"^Microsoft\.Extensions\.Hosting")
            )
            .Check(Architecture);
    }

    [Fact]
    public void RequestHandlers_Should_ResideInApplicationAssembly()
    {
        // ArchUnitNET's fluent predicates don't reliably target open generic interfaces, so
        // this one rule uses plain reflection instead — mirrors the webapi/grpc precedent.
        var handlerTypes = new[]
        {
            typeof(TodoItem).Assembly,
            typeof(ProcessPendingTodoItemsCommand).Assembly,
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
                    "CleanArchWorkerService.Application",
                    handlerType.Namespace,
                    StringComparison.Ordinal
                )
        );
    }
}
