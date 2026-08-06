using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dorn.Abstractions.Generation;
using Dorn.Core.DependencyInjection;
using Dorn.Core.Templating;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace TemplateGenerationTests;

/// <summary>
/// Generates the real webapi template outside the repo and builds it to verify the template's build isolation.
/// </summary>
[Trait("Category", "Integration")]
public class WebApiTemplateGenerationTests
{
    private const string LocalNuGetFeedEnvironmentVariableName = "DORN_LOCAL_NUGET_FEED";

    [Fact]
    public async Task GenerateAndBuild_DornWebApiTemplate_ProducesBuildableSolution()
    {
        // Reuses Dorn.Core's own TemplateLocator instead of re-implementing the
        // DORN_TEMPLATES_PATH / directory-walk resolution logic here, so this test finds
        // templates/ the exact same way the real engine does in production and in CI.
        var templatesRoot = TemplateLocator.ResolveTemplatesRoot();
        Assert.True(Directory.Exists(templatesRoot));

        var services = new ServiceCollection();
        services.AddDornCore();
        await using var provider = services.BuildServiceProvider();
        var engine = provider.GetRequiredService<IGenerationEngine>();

        var outputDirectory = Path.Combine(Path.GetTempPath(), $"dorn-tests-{Guid.NewGuid():N}");
        try
        {
            var request = new GenerationRequest(
                "dorn-webapi",
                "DornIntegrationTestApp",
                outputDirectory
            );
            var result = await engine.GenerateAsync(request);

            Assert.True(
                result.Success,
                "Template generation failed: "
                    + string.Join("; ", result.Diagnostics.Select(d => d.Message))
            );
            Assert.NotEmpty(result.CreatedFiles);

            var slnFiles = Directory.GetFiles(
                outputDirectory,
                "*.slnx",
                SearchOption.TopDirectoryOnly
            );
            Assert.Single(slnFiles);
            Assert.Equal("DornIntegrationTestApp.slnx", Path.GetFileName(slnFiles[0]));

            var buildResult = await RunDotnetBuildAsync(slnFiles[0]);

            Assert.True(
                buildResult.ExitCode == 0,
                $"dotnet build exited with {buildResult.ExitCode}."
                    + $"{Environment.NewLine}STDOUT:{Environment.NewLine}{buildResult.StdOut}"
                    + $"{Environment.NewLine}STDERR:{Environment.NewLine}{buildResult.StdErr}"
            );
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Verifies generated projects include the local Dorn CLI manifest needed for <c>dotnet dorn</c> without a global install.
    /// </summary>
    [Fact]
    public async Task Generate_DornWebApiTemplate_ShipsLocalToolManifestWithDornCli()
    {
        var templatesRoot = TemplateLocator.ResolveTemplatesRoot();
        Assert.True(Directory.Exists(templatesRoot));

        var services = new ServiceCollection();
        services.AddDornCore();
        await using var provider = services.BuildServiceProvider();
        var engine = provider.GetRequiredService<IGenerationEngine>();

        var outputDirectory = Path.Combine(
            Path.GetTempPath(),
            $"dorn-tools-manifest-{Guid.NewGuid():N}"
        );
        try
        {
            var request = new GenerationRequest(
                "dorn-webapi",
                "DornIntegrationTestManifestApp",
                outputDirectory
            );
            var result = await engine.GenerateAsync(request);

            Assert.True(
                result.Success,
                "Template generation failed: "
                    + string.Join("; ", result.Diagnostics.Select(d => d.Message))
            );

            var manifestPath = Path.Combine(outputDirectory, ".config", "dotnet-tools.json");
            Assert.True(
                File.Exists(manifestPath),
                $"Expected local tool manifest at '{manifestPath}' but it was not generated."
            );

            var manifestJson = File.ReadAllText(manifestPath);
            Assert.Contains("\"dorn.cli\"", manifestJson, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"dorn\"", manifestJson, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"rollForward\"", manifestJson, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Verifies the default <c>Auth=none</c> scaffold emits no auth files, no <c>Jwt</c> config block,
    /// and no auth middleware wiring — protects the byte-identical constraint against the pre-change generator.
    /// </summary>
    [Fact]
    public async Task GenerateAndBuild_DornWebApiTemplateWithNoAuth_EmitsNoAuthArtifacts()
    {
        var templatesRoot = TemplateLocator.ResolveTemplatesRoot();
        Assert.True(Directory.Exists(templatesRoot));

        var services = new ServiceCollection();
        services.AddDornCore();
        await using var provider = services.BuildServiceProvider();
        var engine = provider.GetRequiredService<IGenerationEngine>();

        var outputDirectory = Path.Combine(Path.GetTempPath(), $"dorn-noauth-{Guid.NewGuid():N}");
        try
        {
            var request = new GenerationRequest(
                "dorn-webapi",
                "DornNoAuthApp",
                outputDirectory,
                Parameters: new Dictionary<string, string> { ["Auth"] = "none" }
            );
            var result = await engine.GenerateAsync(request);

            Assert.True(
                result.Success,
                "Template generation failed: "
                    + string.Join("; ", result.Diagnostics.Select(d => d.Message))
            );

            var webApiDir = Path.Combine(outputDirectory, "src", "DornNoAuthApp.WebApi");
            Assert.False(
                File.Exists(Path.Combine(webApiDir, "Extensions", "AuthenticationExtensions.cs")),
                "Auth=none must not emit AuthenticationExtensions.cs"
            );
            Assert.False(
                File.Exists(Path.Combine(webApiDir, "Endpoints", "MeEndpoints.cs")),
                "Auth=none must not emit MeEndpoints.cs"
            );

            var appsettingsPath = Path.Combine(webApiDir, "appsettings.json");
            Assert.True(File.Exists(appsettingsPath));
            var appsettings = await File.ReadAllTextAsync(appsettingsPath);
            Assert.DoesNotContain("\"Jwt\"", appsettings, StringComparison.Ordinal);
            Assert.DoesNotContain("SigningKey", appsettings, StringComparison.Ordinal);

            var programCsPath = Path.Combine(webApiDir, "Program.cs");
            Assert.True(File.Exists(programCsPath));
            var programCs = await File.ReadAllTextAsync(programCsPath);
            Assert.DoesNotContain("UseAuthentication", programCs, StringComparison.Ordinal);
            Assert.DoesNotContain("UseAuthorization(", programCs, StringComparison.Ordinal);
            Assert.DoesNotContain("MapMeEndpoints", programCs, StringComparison.Ordinal);

            var csprojPath = Path.Combine(webApiDir, "DornNoAuthApp.WebApi.csproj");
            Assert.True(File.Exists(csprojPath));
            var csproj = await File.ReadAllTextAsync(csprojPath);
            Assert.DoesNotContain(
                "Microsoft.AspNetCore.Authentication.JwtBearer",
                csproj,
                StringComparison.Ordinal
            );
            Assert.DoesNotContain(
                "Microsoft.Extensions.Identity.Core",
                csproj,
                StringComparison.Ordinal
            );

            var slnFiles = Directory.GetFiles(
                outputDirectory,
                "*.slnx",
                SearchOption.TopDirectoryOnly
            );
            Assert.Single(slnFiles);
            var buildResult = await RunDotnetBuildAsync(slnFiles[0]);

            Assert.True(
                buildResult.ExitCode == 0,
                $"dotnet build exited with {buildResult.ExitCode}."
                    + $"{Environment.NewLine}STDOUT:{Environment.NewLine}{buildResult.StdOut}"
                    + $"{Environment.NewLine}STDERR:{Environment.NewLine}{buildResult.StdErr}"
            );
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Verifies the <c>Auth=azure-ad</c> scaffold emits the auth files, the <c>Jwt</c> block in appsettings,
    /// the JwtBearer PackageReference, the middleware wiring, and builds.
    /// </summary>
    [Fact]
    public async Task GenerateAndBuild_DornWebApiTemplateWithAzureAd_EmitsAuthArtifactsAndBuilds()
    {
        var templatesRoot = TemplateLocator.ResolveTemplatesRoot();
        Assert.True(Directory.Exists(templatesRoot));

        var services = new ServiceCollection();
        services.AddDornCore();
        await using var provider = services.BuildServiceProvider();
        var engine = provider.GetRequiredService<IGenerationEngine>();

        var outputDirectory = Path.Combine(Path.GetTempPath(), $"dorn-azuread-{Guid.NewGuid():N}");
        try
        {
            var request = new GenerationRequest(
                "dorn-webapi",
                "DornAzureAdApp",
                outputDirectory,
                Parameters: new Dictionary<string, string> { ["Auth"] = "azure-ad" }
            );
            var result = await engine.GenerateAsync(request);

            Assert.True(
                result.Success,
                "Template generation failed: "
                    + string.Join("; ", result.Diagnostics.Select(d => d.Message))
            );

            var webApiDir = Path.Combine(outputDirectory, "src", "DornAzureAdApp.WebApi");
            Assert.True(
                File.Exists(Path.Combine(webApiDir, "Extensions", "AuthenticationExtensions.cs")),
                "Auth=azure-ad must emit AuthenticationExtensions.cs"
            );
            Assert.True(
                File.Exists(Path.Combine(webApiDir, "Endpoints", "MeEndpoints.cs")),
                "Auth=azure-ad must emit MeEndpoints.cs"
            );

            var appsettingsPath = Path.Combine(webApiDir, "appsettings.json");
            var appsettings = await File.ReadAllTextAsync(appsettingsPath);
            Assert.Contains("\"Jwt\"", appsettings, StringComparison.Ordinal);

            var programCsPath = Path.Combine(webApiDir, "Program.cs");
            var programCs = await File.ReadAllTextAsync(programCsPath);
            Assert.Contains("UseAuthentication", programCs, StringComparison.Ordinal);
            Assert.Contains("MapMeEndpoints", programCs, StringComparison.Ordinal);

            var csprojPath = Path.Combine(webApiDir, "DornAzureAdApp.WebApi.csproj");
            var csproj = await File.ReadAllTextAsync(csprojPath);
            Assert.Contains(
                "Microsoft.AspNetCore.Authentication.JwtBearer",
                csproj,
                StringComparison.Ordinal
            );
            Assert.DoesNotContain(
                "Microsoft.Extensions.Identity.Core",
                csproj,
                StringComparison.Ordinal
            );

            var slnFiles = Directory.GetFiles(
                outputDirectory,
                "*.slnx",
                SearchOption.TopDirectoryOnly
            );
            Assert.Single(slnFiles);
            var buildResult = await RunDotnetBuildAsync(slnFiles[0]);

            Assert.True(
                buildResult.ExitCode == 0,
                $"dotnet build exited with {buildResult.ExitCode}."
                    + $"{Environment.NewLine}STDOUT:{Environment.NewLine}{buildResult.StdOut}"
                    + $"{Environment.NewLine}STDERR:{Environment.NewLine}{buildResult.StdErr}"
            );
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Catches migration namespace collisions, bad #if/Condition/rename modifiers, and stray
    /// //#if markers in appsettings.json.
    /// </summary>
    [Fact]
    public async Task GenerateAndBuild_DornWebApiTemplateWithSqlServer_ProducesBuildableSolution()
    {
        var templatesRoot = TemplateLocator.ResolveTemplatesRoot();
        Assert.True(Directory.Exists(templatesRoot));

        var services = new ServiceCollection();
        services.AddDornCore();
        await using var provider = services.BuildServiceProvider();
        var engine = provider.GetRequiredService<IGenerationEngine>();

        var outputDirectory = Path.Combine(Path.GetTempPath(), $"dorn-tests-{Guid.NewGuid():N}");
        try
        {
            var request = new GenerationRequest(
                "dorn-webapi",
                "DornIntegrationTestSqlServerApp",
                outputDirectory,
                Parameters: new Dictionary<string, string> { ["DatabaseProvider"] = "sqlserver" }
            );
            var result = await engine.GenerateAsync(request);

            Assert.True(
                result.Success,
                "Template generation failed: "
                    + string.Join("; ", result.Diagnostics.Select(d => d.Message))
            );
            Assert.NotEmpty(result.CreatedFiles);

            var migrationsDirectory = Path.Combine(
                outputDirectory,
                "src",
                "DornIntegrationTestSqlServerApp.Infrastructure",
                "Persistence",
                "Migrations"
            );
            Assert.True(Directory.Exists(migrationsDirectory));
            Assert.False(Directory.Exists(Path.Combine(migrationsDirectory, "Sqlite")));
            Assert.False(Directory.Exists(Path.Combine(migrationsDirectory, "SqlServer")));
            Assert.Single(
                Directory.GetFiles(migrationsDirectory, "*ModelSnapshot.cs"),
                path => Path.GetFileName(path) == "ApplicationDbContextModelSnapshot.cs"
            );

            var slnFiles = Directory.GetFiles(
                outputDirectory,
                "*.slnx",
                SearchOption.TopDirectoryOnly
            );
            Assert.Single(slnFiles);

            // The aspire .slnx now correctly references AppHost (Orchestrator symbol fix), so a
            // single build via the solution already compiles AppHost + Aspire.Hosting.SqlServer +
            // the #if (UseSqlServer) wiring in AppHost.cs/.csproj — no separate direct-csproj
            // build is needed anymore.
            Assert.Contains("AppHost", await File.ReadAllTextAsync(slnFiles[0]));

            var buildResult = await RunDotnetBuildAsync(slnFiles[0]);

            Assert.True(
                buildResult.ExitCode == 0,
                $"dotnet build exited with {buildResult.ExitCode}."
                    + $"{Environment.NewLine}STDOUT:{Environment.NewLine}{buildResult.StdOut}"
                    + $"{Environment.NewLine}STDERR:{Environment.NewLine}{buildResult.StdErr}"
            );
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    /// <summary>Mirrors the sqlserver cell above (namespace collisions, #if/Condition/rename modifiers, stray markers).</summary>
    [Fact]
    public async Task GenerateAndBuild_DornWebApiTemplateWithPostgres_ProducesBuildableSolution()
    {
        var templatesRoot = TemplateLocator.ResolveTemplatesRoot();
        Assert.True(Directory.Exists(templatesRoot));

        var services = new ServiceCollection();
        services.AddDornCore();
        await using var provider = services.BuildServiceProvider();
        var engine = provider.GetRequiredService<IGenerationEngine>();

        var outputDirectory = Path.Combine(Path.GetTempPath(), $"dorn-tests-{Guid.NewGuid():N}");
        try
        {
            var request = new GenerationRequest(
                "dorn-webapi",
                "DornIntegrationTestPostgresApp",
                outputDirectory,
                Parameters: new Dictionary<string, string> { ["DatabaseProvider"] = "postgres" }
            );
            var result = await engine.GenerateAsync(request);

            Assert.True(
                result.Success,
                "Template generation failed: "
                    + string.Join("; ", result.Diagnostics.Select(d => d.Message))
            );
            Assert.NotEmpty(result.CreatedFiles);

            var migrationsDirectory = Path.Combine(
                outputDirectory,
                "src",
                "DornIntegrationTestPostgresApp.Infrastructure",
                "Persistence",
                "Migrations"
            );
            Assert.True(Directory.Exists(migrationsDirectory));
            Assert.False(Directory.Exists(Path.Combine(migrationsDirectory, "Sqlite")));
            Assert.False(Directory.Exists(Path.Combine(migrationsDirectory, "SqlServer")));
            Assert.False(Directory.Exists(Path.Combine(migrationsDirectory, "Postgres")));
            Assert.Single(
                Directory.GetFiles(migrationsDirectory, "*ModelSnapshot.cs"),
                path => Path.GetFileName(path) == "ApplicationDbContextModelSnapshot.cs"
            );

            var slnFiles = Directory.GetFiles(
                outputDirectory,
                "*.slnx",
                SearchOption.TopDirectoryOnly
            );
            Assert.Single(slnFiles);
            Assert.Contains("AppHost", await File.ReadAllTextAsync(slnFiles[0]));

            var buildResult = await RunDotnetBuildAsync(slnFiles[0]);

            Assert.True(
                buildResult.ExitCode == 0,
                $"dotnet build exited with {buildResult.ExitCode}."
                    + $"{Environment.NewLine}STDOUT:{Environment.NewLine}{buildResult.StdOut}"
                    + $"{Environment.NewLine}STDERR:{Environment.NewLine}{buildResult.StdErr}"
            );
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Verifies EF-only migrations are excluded and DapperContext.cs has real Npgsql wiring —
    /// source-level only, not a nested build: `dotnet build` from inside this xunit host is
    /// unreliable for CPM resolution through ProjectReference (fails here, succeeds from a plain
    /// shell — a nested dotnet-in-dotnet-test artifact, not a real defect). Compile-correctness
    /// was independently confirmed via a raw UseDapper=true/UsePostgres=true copy (see
    /// apply-progress Work Unit Evidence).
    /// </summary>
    [Fact]
    public async Task Generate_DornWebApiTemplateWithPostgresAndDapper_ExcludesEfOnlyMigrationsAndWiresNpgsql()
    {
        var templatesRoot = TemplateLocator.ResolveTemplatesRoot();
        Assert.True(Directory.Exists(templatesRoot));

        var services = new ServiceCollection();
        services.AddDornCore();
        await using var provider = services.BuildServiceProvider();
        var engine = provider.GetRequiredService<IGenerationEngine>();

        var outputDirectory = Path.Combine(Path.GetTempPath(), $"dorn-tests-{Guid.NewGuid():N}");
        try
        {
            var request = new GenerationRequest(
                "dorn-webapi",
                "DornIntegrationTestPostgresDapperApp",
                outputDirectory,
                Parameters: new Dictionary<string, string>
                {
                    ["DatabaseProvider"] = "postgres",
                    ["Orm"] = "dapper",
                }
            );
            var result = await engine.GenerateAsync(request);

            Assert.True(
                result.Success,
                "Template generation failed: "
                    + string.Join("; ", result.Diagnostics.Select(d => d.Message))
            );
            Assert.NotEmpty(result.CreatedFiles);

            var infrastructureDirectory = Path.Combine(
                outputDirectory,
                "src",
                "DornIntegrationTestPostgresDapperApp.Infrastructure"
            );
            Assert.False(
                Directory.Exists(Path.Combine(infrastructureDirectory, "Persistence", "Migrations"))
            );
            Assert.False(
                Directory.Exists(Path.Combine(infrastructureDirectory, "Repositories", "EfCore"))
            );
            Assert.True(
                Directory.Exists(Path.Combine(infrastructureDirectory, "Repositories", "Dapper"))
            );

            var dapperContextPath = Path.Combine(
                infrastructureDirectory,
                "Repositories",
                "Dapper",
                "DapperContext.cs"
            );
            Assert.True(File.Exists(dapperContextPath));
            var dapperContextSource = await File.ReadAllTextAsync(dapperContextPath);
            Assert.Contains("using Npgsql;", dapperContextSource, StringComparison.Ordinal);
            Assert.Contains("new NpgsqlConnection(", dapperContextSource, StringComparison.Ordinal);
            Assert.DoesNotContain(
                "Postgres provider wiring lands in Slice B",
                dapperContextSource,
                StringComparison.Ordinal
            );

            var infrastructureCsprojSource = await File.ReadAllTextAsync(
                Path.Combine(
                    infrastructureDirectory,
                    "DornIntegrationTestPostgresDapperApp.Infrastructure.csproj"
                )
            );
            Assert.Contains("Npgsql", infrastructureCsprojSource, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    /// <summary>Omits Aspire projects while retaining Docker assets.</summary>
    [Fact]
    public async Task GenerateAndBuild_DornWebApiTemplateWithDockerComposeAndSqlite_ProducesBuildableSolution()
    {
        var templatesRoot = TemplateLocator.ResolveTemplatesRoot();
        Assert.True(Directory.Exists(templatesRoot));

        var services = new ServiceCollection();
        services.AddDornCore();
        await using var provider = services.BuildServiceProvider();
        var engine = provider.GetRequiredService<IGenerationEngine>();

        var outputDirectory = Path.Combine(Path.GetTempPath(), $"dorn-tests-{Guid.NewGuid():N}");
        try
        {
            var request = new GenerationRequest(
                "dorn-webapi",
                "DornIntegrationTestComposeApp",
                outputDirectory,
                Parameters: new Dictionary<string, string> { ["Orchestrator"] = "docker-compose" }
            );
            var result = await engine.GenerateAsync(request);

            Assert.True(
                result.Success,
                "Template generation failed: "
                    + string.Join("; ", result.Diagnostics.Select(d => d.Message))
            );
            Assert.NotEmpty(result.CreatedFiles);

            Assert.False(
                Directory.Exists(
                    Path.Combine(outputDirectory, "src", "DornIntegrationTestComposeApp.AppHost")
                )
            );
            Assert.False(
                Directory.Exists(
                    Path.Combine(
                        outputDirectory,
                        "src",
                        "DornIntegrationTestComposeApp.ServiceDefaults"
                    )
                )
            );
            Assert.True(
                File.Exists(
                    Path.Combine(
                        outputDirectory,
                        "src",
                        "DornIntegrationTestComposeApp.WebApi",
                        "Dockerfile"
                    )
                )
            );
            Assert.True(File.Exists(Path.Combine(outputDirectory, "docker-compose.yml")));

            var slnFiles = Directory.GetFiles(
                outputDirectory,
                "*.slnx",
                SearchOption.TopDirectoryOnly
            );
            Assert.Single(slnFiles);
            Assert.DoesNotContain("AppHost", await File.ReadAllTextAsync(slnFiles[0]));

            var buildResult = await RunDotnetBuildAsync(slnFiles[0]);

            Assert.True(
                buildResult.ExitCode == 0,
                $"dotnet build exited with {buildResult.ExitCode}."
                    + $"{Environment.NewLine}STDOUT:{Environment.NewLine}{buildResult.StdOut}"
                    + $"{Environment.NewLine}STDERR:{Environment.NewLine}{buildResult.StdErr}"
            );
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    /// <summary>Omits orchestration files but retains the Docker assets and solution.</summary>
    [Fact]
    public async Task GenerateAndBuild_DornWebApiTemplateWithNoneOrchestrator_ProducesBuildableSolution()
    {
        var templatesRoot = TemplateLocator.ResolveTemplatesRoot();
        Assert.True(Directory.Exists(templatesRoot));

        var services = new ServiceCollection();
        services.AddDornCore();
        await using var provider = services.BuildServiceProvider();
        var engine = provider.GetRequiredService<IGenerationEngine>();

        var outputDirectory = Path.Combine(Path.GetTempPath(), $"dorn-tests-{Guid.NewGuid():N}");
        try
        {
            var request = new GenerationRequest(
                "dorn-webapi",
                "DornIntegrationTestNoneApp",
                outputDirectory,
                Parameters: new Dictionary<string, string> { ["Orchestrator"] = "none" }
            );
            var result = await engine.GenerateAsync(request);

            Assert.True(
                result.Success,
                "Template generation failed: "
                    + string.Join("; ", result.Diagnostics.Select(d => d.Message))
            );
            Assert.NotEmpty(result.CreatedFiles);

            Assert.False(
                Directory.Exists(
                    Path.Combine(outputDirectory, "src", "DornIntegrationTestNoneApp.AppHost")
                )
            );
            Assert.False(
                Directory.Exists(
                    Path.Combine(
                        outputDirectory,
                        "src",
                        "DornIntegrationTestNoneApp.ServiceDefaults"
                    )
                )
            );
            Assert.True(
                File.Exists(
                    Path.Combine(
                        outputDirectory,
                        "src",
                        "DornIntegrationTestNoneApp.WebApi",
                        "Dockerfile"
                    )
                )
            );
            Assert.True(File.Exists(Path.Combine(outputDirectory, ".dockerignore")));
            Assert.False(File.Exists(Path.Combine(outputDirectory, "docker-compose.yml")));
            Assert.False(
                File.Exists(Path.Combine(outputDirectory, "docker-compose.SqlServer.yml"))
            );

            var slnFiles = Directory.GetFiles(
                outputDirectory,
                "*.slnx",
                SearchOption.TopDirectoryOnly
            );
            Assert.Single(slnFiles);
            Assert.DoesNotContain("AppHost", await File.ReadAllTextAsync(slnFiles[0]));

            var buildResult = await RunDotnetBuildAsync(slnFiles[0]);

            Assert.True(
                buildResult.ExitCode == 0,
                $"dotnet build exited with {buildResult.ExitCode}."
                    + $"{Environment.NewLine}STDOUT:{Environment.NewLine}{buildResult.StdOut}"
                    + $"{Environment.NewLine}STDERR:{Environment.NewLine}{buildResult.StdErr}"
            );
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    /// <summary>Verifies its SQL Server connection override and clean generated settings.</summary>
    [Fact]
    public async Task GenerateAndBuild_DornWebApiTemplateWithDockerComposeAndSqlServer_ProducesBuildableSolution()
    {
        var templatesRoot = TemplateLocator.ResolveTemplatesRoot();
        Assert.True(Directory.Exists(templatesRoot));

        var services = new ServiceCollection();
        services.AddDornCore();
        await using var provider = services.BuildServiceProvider();
        var engine = provider.GetRequiredService<IGenerationEngine>();

        var outputDirectory = Path.Combine(Path.GetTempPath(), $"dorn-tests-{Guid.NewGuid():N}");
        try
        {
            var request = new GenerationRequest(
                "dorn-webapi",
                "DornIntegrationTestComposeSqlServerApp",
                outputDirectory,
                Parameters: new Dictionary<string, string>
                {
                    ["Orchestrator"] = "docker-compose",
                    ["DatabaseProvider"] = "sqlserver",
                }
            );
            var result = await engine.GenerateAsync(request);

            Assert.True(
                result.Success,
                "Template generation failed: "
                    + string.Join("; ", result.Diagnostics.Select(d => d.Message))
            );
            Assert.NotEmpty(result.CreatedFiles);

            var composeFile = Path.Combine(outputDirectory, "docker-compose.yml");
            Assert.True(File.Exists(composeFile));
            var composeContent = await File.ReadAllTextAsync(composeFile);
            Assert.Contains("sqlserver:", composeContent);
            Assert.Contains("ConnectionStrings__", composeContent);

            var migrationsDirectory = Path.Combine(
                outputDirectory,
                "src",
                "DornIntegrationTestComposeSqlServerApp.Infrastructure",
                "Persistence",
                "Migrations"
            );
            Assert.True(Directory.Exists(migrationsDirectory));
            Assert.False(Directory.Exists(Path.Combine(migrationsDirectory, "Sqlite")));
            Assert.False(Directory.Exists(Path.Combine(migrationsDirectory, "SqlServer")));

            var appSettingsContent = await File.ReadAllTextAsync(
                Path.Combine(
                    outputDirectory,
                    "src",
                    "DornIntegrationTestComposeSqlServerApp.WebApi",
                    "appsettings.json"
                )
            );
            Assert.DoesNotContain("//#if", appSettingsContent);

            var slnFiles = Directory.GetFiles(
                outputDirectory,
                "*.slnx",
                SearchOption.TopDirectoryOnly
            );
            Assert.Single(slnFiles);

            var buildResult = await RunDotnetBuildAsync(slnFiles[0]);

            Assert.True(
                buildResult.ExitCode == 0,
                $"dotnet build exited with {buildResult.ExitCode}."
                    + $"{Environment.NewLine}STDOUT:{Environment.NewLine}{buildResult.StdOut}"
                    + $"{Environment.NewLine}STDERR:{Environment.NewLine}{buildResult.StdErr}"
            );
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    /// <summary>Verifies its PostgreSQL connection override and clean generated settings.</summary>
    [Fact]
    public async Task GenerateAndBuild_DornWebApiTemplateWithDockerComposeAndPostgres_ProducesBuildableSolution()
    {
        var templatesRoot = TemplateLocator.ResolveTemplatesRoot();
        Assert.True(Directory.Exists(templatesRoot));

        var services = new ServiceCollection();
        services.AddDornCore();
        await using var provider = services.BuildServiceProvider();
        var engine = provider.GetRequiredService<IGenerationEngine>();

        var outputDirectory = Path.Combine(Path.GetTempPath(), $"dorn-tests-{Guid.NewGuid():N}");
        try
        {
            var request = new GenerationRequest(
                "dorn-webapi",
                "DornIntegrationTestComposePostgresApp",
                outputDirectory,
                Parameters: new Dictionary<string, string>
                {
                    ["Orchestrator"] = "docker-compose",
                    ["DatabaseProvider"] = "postgres",
                }
            );
            var result = await engine.GenerateAsync(request);

            Assert.True(
                result.Success,
                "Template generation failed: "
                    + string.Join("; ", result.Diagnostics.Select(d => d.Message))
            );
            Assert.NotEmpty(result.CreatedFiles);

            var composeFile = Path.Combine(outputDirectory, "docker-compose.yml");
            Assert.True(File.Exists(composeFile));
            var composeContent = await File.ReadAllTextAsync(composeFile);
            Assert.Contains("postgres:", composeContent);
            Assert.Contains("ConnectionStrings__", composeContent);
            Assert.False(
                File.Exists(Path.Combine(outputDirectory, "docker-compose.SqlServer.yml"))
            );

            var migrationsDirectory = Path.Combine(
                outputDirectory,
                "src",
                "DornIntegrationTestComposePostgresApp.Infrastructure",
                "Persistence",
                "Migrations"
            );
            Assert.True(Directory.Exists(migrationsDirectory));
            Assert.False(Directory.Exists(Path.Combine(migrationsDirectory, "Sqlite")));
            Assert.False(Directory.Exists(Path.Combine(migrationsDirectory, "Postgres")));

            var appSettingsContent = await File.ReadAllTextAsync(
                Path.Combine(
                    outputDirectory,
                    "src",
                    "DornIntegrationTestComposePostgresApp.WebApi",
                    "appsettings.json"
                )
            );
            Assert.DoesNotContain("//#if", appSettingsContent);

            var slnFiles = Directory.GetFiles(
                outputDirectory,
                "*.slnx",
                SearchOption.TopDirectoryOnly
            );
            Assert.Single(slnFiles);

            var buildResult = await RunDotnetBuildAsync(slnFiles[0]);

            Assert.True(
                buildResult.ExitCode == 0,
                $"dotnet build exited with {buildResult.ExitCode}."
                    + $"{Environment.NewLine}STDOUT:{Environment.NewLine}{buildResult.StdOut}"
                    + $"{Environment.NewLine}STDERR:{Environment.NewLine}{buildResult.StdErr}"
            );
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task GlobalJson_IsEmittedAtRepositoryRootWithPinnedSdkVersion()
    {
        await WithGeneratedWebApiProjectAsync(
            "DornCiGlobalJsonApp",
            outputDirectory =>
            {
                var nestedGlobalJson = Directory
                    .EnumerateFiles(outputDirectory, "global.json", SearchOption.AllDirectories)
                    .ToList();
                Assert.Single(nestedGlobalJson);

                using var document = ReadJsonFile(outputDirectory, "global.json");
                var sdkVersion = document
                    .RootElement.GetProperty("sdk")
                    .GetProperty("version")
                    .GetString();

                using var dornRootGlobalJson = JsonDocument.Parse(
                    File.ReadAllText(ResolveDornRootGlobalJsonPath())
                );
                var expectedSdkVersion = dornRootGlobalJson
                    .RootElement.GetProperty("sdk")
                    .GetProperty("version")
                    .GetString();

                Assert.Equal(expectedSdkVersion, sdkVersion);
                return Task.CompletedTask;
            }
        );
    }

    /// <summary>
    /// Finds Dorn's root global.json by walking upward so the test works from any checkout.
    /// </summary>
    private static string ResolveDornRootGlobalJsonPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "global.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate dorn's own root global.json by walking up from the test assembly's base directory."
        );
    }

    /// <summary>
    /// Verifies every generation emits a parseable CI workflow with the expected top-level keys.
    /// </summary>
    [Theory]
    [InlineData("aspire")]
    [InlineData("none")]
    public async Task CiWorkflow_IsEmittedAndParses_ForAllSymbols(string orchestrator)
    {
        await WithGeneratedWebApiProjectAsync(
            $"DornCiParse{orchestrator.Replace("-", "", StringComparison.Ordinal)}App",
            outputDirectory =>
            {
                var root = LoadCiWorkflowRoot(outputDirectory);
                Assert.True(root.Children.ContainsKey(new YamlScalarNode("on")));
                Assert.True(root.Children.ContainsKey(new YamlScalarNode("jobs")));
                Assert.Equal("CI", GetScalar(root, "name"));
                return Task.CompletedTask;
            },
            orchestrator: orchestrator,
            databaseProvider: "sqlite"
        );
    }

    /// <summary>
    /// Requirement "Matrix Shape": exactly two axes — os (2 values) and orchestrator (3
    /// values) — for a 6-cell matrix. DatabaseProvider MUST NOT be an axis.
    /// </summary>
    [Fact]
    public async Task CiWorkflow_HasSixCellMatrix()
    {
        await WithGeneratedWebApiProjectAsync(
            "DornCiMatrixApp",
            outputDirectory =>
            {
                var root = LoadCiWorkflowRoot(outputDirectory);
                var jobs = GetMapping(root, "jobs");
                var buildAndTest = GetMapping(jobs, "build-and-test");
                var strategy = GetMapping(buildAndTest, "strategy");
                var matrix = GetMapping(strategy, "matrix");

                var os = GetSequence(matrix, "os");
                var orchestrator = GetSequence(matrix, "orchestrator");

                Assert.Equal(2, os.Children.Count);
                Assert.Equal(3, orchestrator.Children.Count);
                Assert.False(matrix.Children.ContainsKey(new YamlScalarNode("database")));

                return Task.CompletedTask;
            }
        );
    }

    /// <summary>
    /// Requirement "Setup and Cache Steps": checkout@v4 and setup-dotnet@v4 (reading the
    /// repository-root global.json) run before a NuGet cache keyed on Directory.Packages.props.
    /// </summary>
    [Fact]
    public async Task CiWorkflow_PinsSetupAndCacheActions()
    {
        await WithGeneratedWebApiProjectAsync(
            "DornCiSetupCacheApp",
            outputDirectory =>
            {
                var rawText = ReadCiWorkflowRawText(outputDirectory);
                Assert.Contains(
                    "global-json-file: ./global.json",
                    rawText,
                    StringComparison.Ordinal
                );
                Assert.Contains("Directory.Packages.props", rawText, StringComparison.Ordinal);

                var steps = GetSteps(LoadCiWorkflowRoot(outputDirectory), "build-and-test");
                var checkoutIndex = steps.FindIndex(s =>
                    s.Uses?.StartsWith("actions/checkout@v4", StringComparison.Ordinal) == true
                );
                var setupDotnetIndex = steps.FindIndex(s =>
                    s.Uses?.StartsWith("actions/setup-dotnet@v4", StringComparison.Ordinal) == true
                );
                var cacheIndex = steps.FindIndex(s =>
                    s.Uses?.StartsWith("actions/cache@v4", StringComparison.Ordinal) == true
                );

                Assert.True(checkoutIndex >= 0);
                Assert.True(setupDotnetIndex > checkoutIndex);
                Assert.True(cacheIndex > setupDotnetIndex);

                return Task.CompletedTask;
            }
        );
    }

    [Fact]
    public async Task CiWorkflow_RestoresBeforeBuildWithRaceFlags()
    {
        await WithGeneratedWebApiProjectAsync(
            "DornCiRestoreBuildApp",
            outputDirectory =>
            {
                var steps = GetSteps(LoadCiWorkflowRoot(outputDirectory), "build-and-test");
                var restoreIndex = steps.FindIndex(s =>
                    s.Run.Contains("dotnet restore", StringComparison.Ordinal)
                );
                var buildIndex = steps.FindIndex(s =>
                    s.Run.Contains("dotnet build", StringComparison.Ordinal)
                );

                Assert.True(restoreIndex >= 0);
                Assert.True(buildIndex >= 0);
                Assert.True(restoreIndex < buildIndex);

                var restoreCommand = steps[restoreIndex].Run;
                Assert.Contains("-maxCpuCount:1", restoreCommand, StringComparison.Ordinal);
                Assert.Contains("-nodeReuse:false", restoreCommand, StringComparison.Ordinal);

                var buildCommand = steps[buildIndex].Run;
                Assert.Contains("-c Release", buildCommand, StringComparison.Ordinal);
                Assert.Contains("--no-restore", buildCommand, StringComparison.Ordinal);

                return Task.CompletedTask;
            }
        );
    }

    [Fact]
    public async Task CiWorkflow_DefaultTestRunsAllTiersOnce()
    {
        await WithGeneratedWebApiProjectAsync(
            "DornCiDefaultTestApp",
            outputDirectory =>
            {
                var steps = GetSteps(LoadCiWorkflowRoot(outputDirectory), "build-and-test");
                var context = new Dictionary<string, string> { ["inputs.exclude_tiers"] = "" };

                var activeTestCommands = steps
                    .Where(s => s.Run.Contains("dotnet test", StringComparison.Ordinal))
                    .Where(s => s.If is null || EvaluateGithubActionsExpression(s.If, context))
                    .Select(s => s.Run)
                    .ToList();

                var command = Assert.Single(activeTestCommands);
                Assert.Contains("--no-build", command, StringComparison.Ordinal);
                Assert.Contains("-c Release", command, StringComparison.Ordinal);
                Assert.Contains(
                    "--collect:\"XPlat Code Coverage\"",
                    command,
                    StringComparison.Ordinal
                );
                Assert.DoesNotContain("--filter", command, StringComparison.Ordinal);

                return Task.CompletedTask;
            }
        );
    }

    [Fact]
    public async Task CiWorkflow_ExclusionRunsRemainingTiers()
    {
        await WithGeneratedWebApiProjectAsync(
            "DornCiExclusionApp",
            outputDirectory =>
            {
                var steps = GetSteps(LoadCiWorkflowRoot(outputDirectory), "build-and-test");
                var context = new Dictionary<string, string>
                {
                    ["inputs.exclude_tiers"] = "Integration",
                };

                var activeTestSteps = steps
                    .Where(s => s.Run.Contains("dotnet test", StringComparison.Ordinal))
                    .Where(s => s.If is null || EvaluateGithubActionsExpression(s.If, context))
                    .ToList();

                Assert.Equal(3, activeTestSteps.Count);
                Assert.All(activeTestSteps, s => Assert.NotNull(s.If));
                Assert.DoesNotContain(
                    activeTestSteps,
                    s => s.Run.Contains("Integration.Tests", StringComparison.Ordinal)
                );

                return Task.CompletedTask;
            }
        );
    }

    [Fact]
    public async Task CiWorkflow_SqliteStartsNoService()
    {
        await WithGeneratedWebApiProjectAsync(
            "DornCiSqliteServiceApp",
            outputDirectory =>
            {
                var steps = GetSteps(LoadCiWorkflowRoot(outputDirectory), "build-and-test");
                var context = new Dictionary<string, string>
                {
                    ["needs.configuration.outputs.db"] = "sqlite",
                    ["runner.os"] = "Linux",
                };

                var sqlServerSteps = steps
                    .Where(s =>
                        s.Run.Contains("mcr.microsoft.com/azure-sql-edge", StringComparison.Ordinal)
                    )
                    .ToList();
                Assert.NotEmpty(sqlServerSteps);
                Assert.All(
                    sqlServerSteps,
                    s =>
                        Assert.False(
                            s.If is not null && EvaluateGithubActionsExpression(s.If, context),
                            $"Step '{s.Name}' would execute for a sqlite marker."
                        )
                );

                return Task.CompletedTask;
            }
        );
    }

    [Fact]
    public async Task CiWorkflow_LinuxSqlServerUsesHealthyEdge()
    {
        await WithGeneratedWebApiProjectAsync(
            "DornCiLinuxSqlServerApp",
            outputDirectory =>
            {
                var steps = GetSteps(LoadCiWorkflowRoot(outputDirectory), "build-and-test");
                var context = new Dictionary<string, string>
                {
                    ["needs.configuration.outputs.db"] = "sqlserver",
                    ["runner.os"] = "Linux",
                };

                var startIndex = steps.FindIndex(s =>
                    s.Run.Contains("mcr.microsoft.com/azure-sql-edge", StringComparison.Ordinal)
                );
                Assert.True(startIndex >= 0);
                Assert.NotNull(steps[startIndex].If);
                Assert.True(EvaluateGithubActionsExpression(steps[startIndex].If!, context));

                var healthCheckIndex = steps.FindIndex(s =>
                    s.Run.Contains("sqlcmd", StringComparison.Ordinal)
                    && s.Run.Contains("-Q \"select 1\"", StringComparison.Ordinal)
                );
                Assert.True(healthCheckIndex >= 0);

                var testIndex = steps.FindIndex(s =>
                    s.Run.Contains("dotnet test", StringComparison.Ordinal)
                );
                Assert.True(testIndex >= 0);
                Assert.True(healthCheckIndex < testIndex);

                return Task.CompletedTask;
            }
        );
    }

    [Fact]
    public async Task CiWorkflow_LinuxPostgresUsesHealthyContainer()
    {
        await WithGeneratedWebApiProjectAsync(
            "DornCiLinuxPostgresApp",
            outputDirectory =>
            {
                var steps = GetSteps(LoadCiWorkflowRoot(outputDirectory), "build-and-test");
                var context = new Dictionary<string, string>
                {
                    ["needs.configuration.outputs.db"] = "postgres",
                    ["runner.os"] = "Linux",
                };

                var startIndex = steps.FindIndex(s =>
                    s.Run.Contains("postgres:17", StringComparison.Ordinal)
                    && s.Run.Contains("docker run", StringComparison.Ordinal)
                );
                Assert.True(startIndex >= 0);
                Assert.NotNull(steps[startIndex].If);
                Assert.True(EvaluateGithubActionsExpression(steps[startIndex].If!, context));

                var healthCheckIndex = steps.FindIndex(s =>
                    s.Run.Contains("pg_isready", StringComparison.Ordinal)
                );
                Assert.True(healthCheckIndex >= 0);

                var testIndex = steps.FindIndex(s =>
                    s.Run.Contains("dotnet test", StringComparison.Ordinal)
                );
                Assert.True(testIndex >= 0);
                Assert.True(healthCheckIndex < testIndex);

                return Task.CompletedTask;
            }
        );
    }

    [Fact]
    public async Task CiWorkflow_SqliteStartsNoPostgresService()
    {
        await WithGeneratedWebApiProjectAsync(
            "DornCiSqlitePostgresServiceApp",
            outputDirectory =>
            {
                var steps = GetSteps(LoadCiWorkflowRoot(outputDirectory), "build-and-test");
                var context = new Dictionary<string, string>
                {
                    ["needs.configuration.outputs.db"] = "sqlite",
                    ["runner.os"] = "Linux",
                };

                var postgresSteps = steps
                    .Where(s => s.Run.Contains("postgres:17", StringComparison.Ordinal))
                    .ToList();
                Assert.NotEmpty(postgresSteps);
                Assert.All(
                    postgresSteps,
                    s =>
                        Assert.False(
                            s.If is not null && EvaluateGithubActionsExpression(s.If, context),
                            $"Step '{s.Name}' would execute for a sqlite marker."
                        )
                );

                return Task.CompletedTask;
            }
        );
    }

    /// <summary>Uses Testcontainers as a .NET library, not a CLI.</summary>
    [Fact]
    public async Task CiWorkflow_WindowsPostgresIsBestEffort()
    {
        await WithGeneratedWebApiProjectAsync(
            "DornCiWindowsPostgresApp",
            outputDirectory =>
            {
                var rawText = ReadCiWorkflowRawText(outputDirectory);
                var branchIndex = rawText.IndexOf(
                    "Windows + PostgreSQL caveat",
                    StringComparison.Ordinal
                );
                Assert.True(branchIndex >= 0);

                var steps = GetSteps(LoadCiWorkflowRoot(outputDirectory), "build-and-test");
                Assert.All(
                    steps,
                    s =>
                        Assert.DoesNotContain(
                            "testcontainers",
                            s.Run,
                            StringComparison.OrdinalIgnoreCase
                        )
                );

                return Task.CompletedTask;
            }
        );
    }

    /// <summary>Uses Testcontainers as a .NET library, not a CLI.</summary>
    [Fact]
    public async Task CiWorkflow_WindowsSqlServerIsBestEffort()
    {
        await WithGeneratedWebApiProjectAsync(
            "DornCiWindowsSqlServerApp",
            outputDirectory =>
            {
                var rawText = ReadCiWorkflowRawText(outputDirectory);
                var commentIndex = rawText.IndexOf("# best-effort:", StringComparison.Ordinal);
                var branchIndex = rawText.IndexOf(
                    "Windows + SQL Server caveat",
                    StringComparison.Ordinal
                );
                Assert.True(commentIndex >= 0);
                Assert.True(branchIndex > commentIndex);

                var steps = GetSteps(LoadCiWorkflowRoot(outputDirectory), "build-and-test");
                Assert.All(
                    steps,
                    s =>
                        Assert.DoesNotContain(
                            "testcontainers",
                            s.Run,
                            StringComparison.OrdinalIgnoreCase
                        )
                );

                return Task.CompletedTask;
            }
        );
    }

    /// <summary>
    /// Requirement "ORM Compatibility": no hardcoded `dotnet ef` calls anywhere in the
    /// workflow — migrations-on-startup is a runtime concern inside the app, not CI.
    /// </summary>
    [Fact]
    public async Task CiWorkflow_DoesNotInvokeEfCli()
    {
        await WithGeneratedWebApiProjectAsync(
            "DornCiNoEfCliApp",
            outputDirectory =>
            {
                Assert.DoesNotContain(
                    "dotnet ef",
                    ReadCiWorkflowRawText(outputDirectory),
                    StringComparison.Ordinal
                );
                return Task.CompletedTask;
            }
        );
    }

    [Fact]
    public async Task CiWorkflow_AggregatesCoverageOnUbuntuOnly()
    {
        await WithGeneratedWebApiProjectAsync(
            "DornCiCoverageApp",
            outputDirectory =>
            {
                var steps = GetSteps(LoadCiWorkflowRoot(outputDirectory), "build-and-test");
                var coverageStep = steps.Single(s =>
                    s.Run.Contains("reportgenerator", StringComparison.OrdinalIgnoreCase)
                );

                Assert.Contains(
                    "**/coverage.cobertura.xml",
                    coverageStep.Run,
                    StringComparison.Ordinal
                );
                Assert.Contains(
                    "-assemblyfilters:+:-*.Tests",
                    coverageStep.Run,
                    StringComparison.Ordinal
                );
                Assert.Equal("matrix.os == 'ubuntu-latest'", coverageStep.If);

                return Task.CompletedTask;
            }
        );
    }

    /// <summary>
    /// Requirement "Marker File Emission" (SQLite): `--database sqlite` emits
    /// `.github/config/db-provider.txt` equal to `sqlite`.
    /// </summary>
    [Fact]
    public async Task SqliteMarker_IsEmitted()
    {
        await WithGeneratedWebApiProjectAsync(
            "DornCiSqliteMarkerApp",
            outputDirectory =>
            {
                var markerPath = Path.Combine(
                    outputDirectory,
                    ".github",
                    "config",
                    "db-provider.txt"
                );
                Assert.True(File.Exists(markerPath), $"Expected marker file at '{markerPath}'.");
                Assert.Equal("sqlite", File.ReadAllText(markerPath).Trim());
                return Task.CompletedTask;
            },
            databaseProvider: "sqlite"
        );
    }

    /// <summary>
    /// Requirement "Marker File Emission" (SQL Server): `--database sqlserver` emits
    /// `.github/config/db-provider.txt` equal to `sqlserver`.
    /// </summary>
    [Fact]
    public async Task SqlServerMarker_IsEmitted()
    {
        await WithGeneratedWebApiProjectAsync(
            "DornCiSqlServerMarkerApp",
            outputDirectory =>
            {
                var markerPath = Path.Combine(
                    outputDirectory,
                    ".github",
                    "config",
                    "db-provider.txt"
                );
                Assert.True(File.Exists(markerPath), $"Expected marker file at '{markerPath}'.");
                Assert.Equal("sqlserver", File.ReadAllText(markerPath).Trim());
                return Task.CompletedTask;
            },
            databaseProvider: "sqlserver"
        );
    }

    /// <summary>
    /// Requirement "Marker File Emission" (PostgreSQL): `--database postgres` emits
    /// `.github/config/db-provider.txt` equal to `postgres`.
    /// </summary>
    [Fact]
    public async Task PostgresMarker_IsEmitted()
    {
        await WithGeneratedWebApiProjectAsync(
            "DornCiPostgresMarkerApp",
            outputDirectory =>
            {
                var markerPath = Path.Combine(
                    outputDirectory,
                    ".github",
                    "config",
                    "db-provider.txt"
                );
                Assert.True(File.Exists(markerPath), $"Expected marker file at '{markerPath}'.");
                Assert.Equal("postgres", File.ReadAllText(markerPath).Trim());
                return Task.CompletedTask;
            },
            databaseProvider: "postgres"
        );
    }

    /// <summary>No out-of-scope packaging, Dependabot, or README badge steps.</summary>
    [Fact]
    public async Task CiWorkflow_ContainsNoOutOfScopeSteps()
    {
        await WithGeneratedWebApiProjectAsync(
            "DornCiOutOfScopeApp",
            outputDirectory =>
            {
                var rawText = ReadCiWorkflowRawText(outputDirectory);
                Assert.DoesNotContain("dotnet pack", rawText, StringComparison.Ordinal);
                Assert.DoesNotContain("dotnet nuget push", rawText, StringComparison.Ordinal);
                Assert.DoesNotContain("dependabot", rawText, StringComparison.Ordinal);
                Assert.DoesNotContain("badge", rawText, StringComparison.Ordinal);
                return Task.CompletedTask;
            }
        );
    }

    /// <summary>
    /// Re-runs the structural workflow contract for the representative efcore/aspire/sqlite cell.
    /// </summary>
    [Fact]
    public async Task CiWorkflow_StructuralContract_HoldsAcrossMatrix()
    {
        await WithGeneratedWebApiProjectAsync(
            "DornCiAggregateApp",
            outputDirectory =>
            {
                var root = LoadCiWorkflowRoot(outputDirectory);
                Assert.True(root.Children.ContainsKey(new YamlScalarNode("on")));
                Assert.True(root.Children.ContainsKey(new YamlScalarNode("jobs")));

                var on = GetMapping(root, "on");
                Assert.True(on.Children.ContainsKey(new YamlScalarNode("push")));
                Assert.True(on.Children.ContainsKey(new YamlScalarNode("pull_request")));
                Assert.True(on.Children.ContainsKey(new YamlScalarNode("workflow_dispatch")));

                var jobs = GetMapping(root, "jobs");
                var buildAndTest = GetMapping(jobs, "build-and-test");
                var matrix = GetMapping(GetMapping(buildAndTest, "strategy"), "matrix");
                Assert.Equal(2, GetSequence(matrix, "os").Children.Count);
                Assert.Equal(3, GetSequence(matrix, "orchestrator").Children.Count);

                var rawText = ReadCiWorkflowRawText(outputDirectory);
                Assert.DoesNotContain("dotnet ef", rawText, StringComparison.Ordinal);
                Assert.DoesNotContain("dotnet pack", rawText, StringComparison.Ordinal);

                using var globalJson = ReadJsonFile(outputDirectory, "global.json");
                Assert.False(
                    string.IsNullOrWhiteSpace(
                        globalJson.RootElement.GetProperty("sdk").GetProperty("version").GetString()
                    )
                );

                return Task.CompletedTask;
            },
            orm: "efcore",
            orchestrator: "aspire",
            databaseProvider: "sqlite"
        );
    }

    /// <summary>
    /// Builds and tests the representative efcore/none/sqlite cell while validating its workflow YAML.
    /// </summary>
    [Fact]
    public async Task GeneratedCheapestCell_BuildsTestsAndHasValidWorkflow()
    {
        await WithGeneratedWebApiProjectAsync(
            "DornCiSmokeApp",
            async outputDirectory =>
            {
                // Structural YAML-parser check, independent of the assertions above.
                LoadCiWorkflowRoot(outputDirectory);

                var slnFiles = Directory.GetFiles(
                    outputDirectory,
                    "*.slnx",
                    SearchOption.TopDirectoryOnly
                );
                Assert.Single(slnFiles);

                var buildResult = await RunDotnetBuildAsync(slnFiles[0]);
                Assert.True(
                    buildResult.ExitCode == 0,
                    $"dotnet build exited with {buildResult.ExitCode}."
                        + $"{Environment.NewLine}STDOUT:{Environment.NewLine}{buildResult.StdOut}"
                        + $"{Environment.NewLine}STDERR:{Environment.NewLine}{buildResult.StdErr}"
                );

                var testResult = await RunProcessAsync(
                    slnFiles[0],
                    "test",
                    slnFiles[0],
                    "-c",
                    "Release",
                    "--no-build"
                );
                Assert.True(
                    testResult.ExitCode == 0,
                    $"dotnet test exited with {testResult.ExitCode}."
                        + $"{Environment.NewLine}STDOUT:{Environment.NewLine}{testResult.StdOut}"
                        + $"{Environment.NewLine}STDERR:{Environment.NewLine}{testResult.StdErr}"
                );
            },
            orm: "efcore",
            orchestrator: "none",
            databaseProvider: "sqlite"
        );
    }

    private static List<(string? Name, string? Uses, string? If, string Run)> GetSteps(
        YamlMappingNode root,
        string jobName
    )
    {
        var jobs = GetMapping(root, "jobs");
        var job = GetMapping(jobs, jobName);
        var steps = GetSequence(job, "steps");

        var result = new List<(string? Name, string? Uses, string? If, string Run)>();
        foreach (var stepNode in steps.Children)
        {
            var step = (YamlMappingNode)stepNode;
            var name = TryGetChild(step, "name") is YamlScalarNode nameNode ? nameNode.Value : null;
            var uses = TryGetChild(step, "uses") is YamlScalarNode usesNode ? usesNode.Value : null;
            var ifValue = TryGetChild(step, "if") is YamlScalarNode ifNode ? ifNode.Value : null;
            var runValue = TryGetChild(step, "run") is YamlScalarNode runNode
                ? runNode.Value ?? string.Empty
                : string.Empty;
            result.Add((name, uses, ifValue, runValue));
        }

        return result;
    }

    /// <summary>
    /// Evaluates the subset of GitHub Actions <c>if:</c> expressions used by the workflow without running GitHub Actions.
    /// </summary>
    private static bool EvaluateGithubActionsExpression(
        string expression,
        IReadOnlyDictionary<string, string> context
    )
    {
        foreach (var rawClause in expression.Split("&&"))
        {
            var clause = rawClause.Trim();
            var negate = clause.StartsWith('!');
            if (negate)
            {
                clause = clause[1..].Trim();
            }

            bool clauseResult;
            var containsMatch = Regex.Match(
                clause,
                @"^contains\((?<expr>[^,]+),\s*'(?<value>[^']*)'\)$"
            );
            if (containsMatch.Success)
            {
                var left = ResolveGithubActionsExpressionValue(
                    containsMatch.Groups["expr"].Value.Trim(),
                    context
                );
                clauseResult = left.Contains(
                    containsMatch.Groups["value"].Value,
                    StringComparison.Ordinal
                );
            }
            else
            {
                var comparisonMatch = Regex.Match(
                    clause,
                    @"^(?<left>[A-Za-z0-9_.]+)\s*(?<op>==|!=)\s*'(?<value>[^']*)'$"
                );
                if (!comparisonMatch.Success)
                {
                    throw new NotSupportedException(
                        $"Unsupported GitHub Actions expression clause: '{clause}'."
                    );
                }

                var left = ResolveGithubActionsExpressionValue(
                    comparisonMatch.Groups["left"].Value,
                    context
                );
                var equal = string.Equals(
                    left,
                    comparisonMatch.Groups["value"].Value,
                    StringComparison.Ordinal
                );
                clauseResult = comparisonMatch.Groups["op"].Value == "==" ? equal : !equal;
            }

            if (negate)
            {
                clauseResult = !clauseResult;
            }

            if (!clauseResult)
            {
                return false;
            }
        }

        return true;
    }

    private static string ResolveGithubActionsExpressionValue(
        string reference,
        IReadOnlyDictionary<string, string> context
    )
    {
        var normalized = reference.Replace(
            "github.event.inputs.",
            "inputs.",
            StringComparison.Ordinal
        );
        return context.TryGetValue(normalized, out var value) ? value : string.Empty;
    }

    /// <summary>
    /// Requirement "Triggers": the emitted workflow must trigger on push, pull_request, and
    /// workflow_dispatch only — no cron schedule, no path filters.
    /// </summary>
    [Fact]
    public async Task CiWorkflow_DeclaresExactlySupportedTriggers()
    {
        await WithGeneratedWebApiProjectAsync(
            "DornCiTriggersApp",
            outputDirectory =>
            {
                var root = LoadCiWorkflowRoot(outputDirectory);
                var on = GetMapping(root, "on");

                Assert.True(on.Children.ContainsKey(new YamlScalarNode("push")));
                Assert.True(on.Children.ContainsKey(new YamlScalarNode("pull_request")));
                Assert.True(on.Children.ContainsKey(new YamlScalarNode("workflow_dispatch")));
                Assert.False(on.Children.ContainsKey(new YamlScalarNode("schedule")));

                var rawText = ReadCiWorkflowRawText(outputDirectory);
                Assert.DoesNotContain("paths:", rawText, StringComparison.Ordinal);
                Assert.DoesNotContain("paths-ignore:", rawText, StringComparison.Ordinal);

                return Task.CompletedTask;
            }
        );
    }

    private static string GetCiWorkflowPath(string outputDirectory) =>
        Path.Combine(outputDirectory, ".github", "workflows", "ci.yml");

    private static string ReadCiWorkflowRawText(string outputDirectory) =>
        File.ReadAllText(GetCiWorkflowPath(outputDirectory));

    private static YamlMappingNode LoadCiWorkflowRoot(string outputDirectory)
    {
        var path = GetCiWorkflowPath(outputDirectory);
        Assert.True(File.Exists(path), $"Expected CI workflow at '{path}'.");

        var yaml = new YamlStream();
        using var reader = new StringReader(File.ReadAllText(path));
        yaml.Load(reader);
        return (YamlMappingNode)yaml.Documents[0].RootNode;
    }

    private static YamlNode? TryGetChild(YamlMappingNode node, string key) =>
        node.Children.TryGetValue(new YamlScalarNode(key), out var value) ? value : null;

    private static YamlMappingNode GetMapping(YamlMappingNode node, string key)
    {
        var child = TryGetChild(node, key);
        Assert.NotNull(child);
        return Assert.IsType<YamlMappingNode>(child);
    }

    private static YamlSequenceNode GetSequence(YamlMappingNode node, string key)
    {
        var child = TryGetChild(node, key);
        Assert.NotNull(child);
        return Assert.IsType<YamlSequenceNode>(child);
    }

    private static string GetScalar(YamlMappingNode node, string key)
    {
        var child = TryGetChild(node, key);
        Assert.NotNull(child);
        return Assert.IsType<YamlScalarNode>(child).Value ?? string.Empty;
    }

    /// <summary>
    /// Restores before building to avoid MSBuild restore races; retries the known race signature and uses the local feed for generated projects.
    /// </summary>
    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunDotnetBuildAsync(
        string solutionPath
    )
    {
        var restoreResult = await RestoreWithRetryAsync(solutionPath);
        if (restoreResult.ExitCode != 0)
        {
            return restoreResult;
        }

        return await RunProcessAsync(
            solutionPath,
            "build",
            solutionPath,
            "-c",
            "Release",
            "--no-restore",
            "-nodeReuse:false"
        );
    }

    /// <summary>
    /// Retries restore only for the known concurrent generated-file race; other failures return immediately.
    /// </summary>
    private static async Task<(int ExitCode, string StdOut, string StdErr)> RestoreWithRetryAsync(
        string solutionPath,
        int maxAttempts = 3
    )
    {
        var localNuGetFeed = ResolveLocalNuGetFeed();
        (int ExitCode, string StdOut, string StdErr) result = (1, "", "");

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            result = await RunProcessAsync(
                solutionPath,
                "restore",
                solutionPath,
                $"-p:RestoreAdditionalProjectSources={localNuGetFeed}",
                "-nodeReuse:false",
                "-maxCpuCount:1"
            );

            if (result.ExitCode == 0)
            {
                return result;
            }

            // MSBuild writes this specific error to stdout (its console logger), not stderr.
            var isKnownRace =
                result.StdOut.Contains("already exists", StringComparison.OrdinalIgnoreCase)
                || result.StdErr.Contains("already exists", StringComparison.OrdinalIgnoreCase);
            if (!isKnownRace || attempt == maxAttempts)
            {
                return result;
            }
        }

        return result;
    }

    /// <summary>
    /// Resolves dorn's local NuGet feed (./artifacts) via env var or directory walk
    /// fallback — same pattern as TemplateLocator.ResolveTemplatesRoot.
    /// </summary>
    private static string ResolveLocalNuGetFeed()
    {
        var envOverride = Environment.GetEnvironmentVariable(LocalNuGetFeedEnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(envOverride))
        {
            return Path.GetFullPath(envOverride);
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "artifacts");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate Dorn's local NuGet feed. Set the {LocalNuGetFeedEnvironmentVariableName} "
                + "environment variable to point at the repo's 'artifacts' directory (see "
                + "eng/scripts/pack-packages.ps1), or run the tests from a repo checkout that already has one."
        );
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunProcessAsync(
        string solutionPath,
        params string[] arguments
    )
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(solutionPath)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process =
            Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the nested dotnet process.");

        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, await stdOutTask, await stdErrTask);
    }

    /// <summary>
    /// Generates a fresh webapi project with optional symbols, using template defaults for omitted values.
    /// </summary>
    private static async Task<string> GenerateWebApiProjectAsync(
        string projectName,
        string? orm = null,
        string? orchestrator = null,
        string? databaseProvider = null
    )
    {
        var templatesRoot = TemplateLocator.ResolveTemplatesRoot();
        Assert.True(Directory.Exists(templatesRoot));

        var services = new ServiceCollection();
        services.AddDornCore();
        await using var provider = services.BuildServiceProvider();
        var engine = provider.GetRequiredService<IGenerationEngine>();

        var parameters = new Dictionary<string, string>();
        if (orm is not null)
        {
            parameters["Orm"] = orm;
        }
        if (orchestrator is not null)
        {
            parameters["Orchestrator"] = orchestrator;
        }
        if (databaseProvider is not null)
        {
            parameters["DatabaseProvider"] = databaseProvider;
        }

        var outputDirectory = Path.Combine(Path.GetTempPath(), $"dorn-ci-tests-{Guid.NewGuid():N}");
        var request = new GenerationRequest(
            "dorn-webapi",
            projectName,
            outputDirectory,
            Parameters: parameters.Count > 0 ? parameters : null
        );
        var result = await engine.GenerateAsync(request);

        Assert.True(
            result.Success,
            "Template generation failed: "
                + string.Join("; ", result.Diagnostics.Select(d => d.Message))
        );

        return outputDirectory;
    }

    /// <summary>
    /// Generates a project, runs the test body, and always cleans up its temporary directory.
    /// </summary>
    private static async Task WithGeneratedWebApiProjectAsync(
        string projectName,
        Func<string, Task> body,
        string? orm = null,
        string? orchestrator = null,
        string? databaseProvider = null
    )
    {
        var outputDirectory = await GenerateWebApiProjectAsync(
            projectName,
            orm,
            orchestrator,
            databaseProvider
        );
        try
        {
            await body(outputDirectory);
        }
        finally
        {
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    /// <summary>
    /// Reads and parses generated JSON at <paramref name="relativePath"/> (relative to the
    /// generated project's output directory) as a <see cref="JsonDocument"/>.
    /// </summary>
    private static JsonDocument ReadJsonFile(string outputDirectory, params string[] relativePath)
    {
        var path = Path.Combine([outputDirectory, .. relativePath]);
        Assert.True(File.Exists(path), $"Expected generated file at '{path}'.");
        return JsonDocument.Parse(File.ReadAllText(path));
    }
}
