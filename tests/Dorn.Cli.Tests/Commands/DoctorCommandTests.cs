using Dorn.Cli.Commands.Doctor;
using Dorn.Cli.Execution;
using Dorn.Cli.Projects;
using Dorn.Cli.Templating;
using Dorn.Cli.Theming;
using NSubstitute;
using Spectre.Console.Testing;
using Xunit;

namespace Dorn.Cli.Tests.Commands;

///<summary>
/// Tests for <see cref="DoctorCommand"/>. Runs RunAsync directly (CommandAppTester removed
/// in Spectre.Console.Cli 0.55.0). Uses a real <see cref="TestConsole"/> so rendered table
/// content can be asserted, NSubstitute for <see cref="ITemplatesRootLocator"/>/
/// <see cref="IProcessRunner"/>, and the real <see cref="ProjectContextResolver"/> over temp
/// directories (mirrors <c>TestCommandTests</c>). No DORN_TEMPLATES_PATH mutation anywhere in
/// this assembly.
///</summary>
public class DoctorCommandTests : IDisposable
{
    private const string PassingSdkVersion = "10.0.301\n";

    private readonly string _tempRoot;

    public DoctorCommandTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"dorn-doctor-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    // Templates check

    [Fact]
    public async Task Doctor_TemplatesRootResolves_RendersPassRowWithPath()
    {
        var (templatesRootLocator, _, _, console, command) = CreateCommand();
        templatesRootLocator.Resolve().Returns("/opt/dorn/templates");

        var exitCode = await command.RunAsync(
            new DoctorSettings { Project = _tempRoot },
            CancellationToken.None
        );

        Assert.Equal(0, exitCode);
        Assert.Contains("PASS", console.Output);
        Assert.Contains("/opt/dorn/templates", console.Output);
    }

    [Fact]
    public async Task Doctor_TemplatesRootMissing_RendersFailRowWithRemediationAndNoException()
    {
        var (templatesRootLocator, _, _, console, command) = CreateCommand();
        templatesRootLocator
            .Resolve()
            .Returns(_ => throw new DirectoryNotFoundException("Could not locate templates."));

        var exitCode = await command.RunAsync(
            new DoctorSettings { Project = _tempRoot },
            CancellationToken.None
        );

        Assert.Equal(1, exitCode);
        Assert.Contains("FAIL", console.Output);
        Assert.Contains("DORN_TEMPLATES_PATH", console.Output);
    }

    [Fact]
    public async Task Doctor_TemplatesRootUnauthorized_RendersFailRowInsteadOfCrashing()
    {
        var (templatesRootLocator, _, _, console, command) = CreateCommand();
        templatesRootLocator
            .Resolve()
            .Returns(_ => throw new UnauthorizedAccessException("no access to templates dir"));

        var exitCode = await command.RunAsync(
            new DoctorSettings { Project = _tempRoot },
            CancellationToken.None
        );

        Assert.Equal(1, exitCode);
        Assert.Contains("FAIL", console.Output);
        Assert.Contains("DORN_TEMPLATES_PATH", console.Output);
    }

    // Dotnet SDK check

    [Theory]
    [InlineData("10.0.400\n")]
    [InlineData("11.0.100\n")]
    public async Task Doctor_SdkNewerThanBaseline_PassesSilentlyWithNoWarnText(
        string reportedVersion
    )
    {
        var (_, processRunner, _, console, command) = CreateCommand();
        StubDotnetVersion(processRunner, 0, reportedVersion);

        var exitCode = await command.RunAsync(
            new DoctorSettings { Project = _tempRoot },
            CancellationToken.None
        );

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("WARN", console.Output);
    }

    [Fact]
    public async Task Doctor_SdkOlderThanBaseline_RendersFailWithActualAndExpectedVersions()
    {
        var (_, processRunner, _, console, command) = CreateCommand();
        StubDotnetVersion(processRunner, 0, "9.0.100\n");

        var exitCode = await command.RunAsync(
            new DoctorSettings { Project = _tempRoot },
            CancellationToken.None
        );

        Assert.Equal(1, exitCode);
        Assert.Contains("FAIL", console.Output);
        Assert.Contains("9.0.100", console.Output);
        Assert.Contains("10.0.301", console.Output);
    }

    [Fact]
    public async Task Doctor_DotnetMissingFromPath_RendersFailMentioningPath()
    {
        var (_, processRunner, _, console, command) = CreateCommand();
        StubDotnetVersion(processRunner, 127, string.Empty);

        var exitCode = await command.RunAsync(
            new DoctorSettings { Project = _tempRoot },
            CancellationToken.None
        );

        Assert.Equal(1, exitCode);
        Assert.Contains("FAIL", console.Output);
        Assert.Contains("PATH", console.Output);
    }

    [Fact]
    public async Task Doctor_GarbageDotnetStdout_RendersFailWithoutThrowingMarkupException()
    {
        var (_, processRunner, _, console, command) = CreateCommand();
        StubDotnetVersion(processRunner, 0, "[[not-a-version]] bogus\n");

        var exitCode = await command.RunAsync(
            new DoctorSettings { Project = _tempRoot },
            CancellationToken.None
        );

        Assert.Equal(1, exitCode);
        Assert.Contains("FAIL", console.Output);
    }

    // Docker advisory check (Compose-only)

    [Fact]
    public async Task Doctor_ComposeProject_RendersDockerRow()
    {
        var (_, processRunner, _, console, command) = CreateCommand();
        CreateComposeFile();
        StubDockerVersion(processRunner, 0, "Docker version 27.0.0\n");

        var exitCode = await command.RunAsync(
            new DoctorSettings { Project = _tempRoot },
            CancellationToken.None
        );

        Assert.Equal(0, exitCode);
        Assert.Contains("Docker", console.Output);
    }

    [Fact]
    public async Task Doctor_NonComposeProject_HidesDockerRowAndNeverProbesDocker()
    {
        var (_, processRunner, _, console, command) = CreateCommand();

        var exitCode = await command.RunAsync(
            new DoctorSettings { Project = _tempRoot },
            CancellationToken.None
        );

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("Docker", console.Output);
        await processRunner
            .DidNotReceive()
            .RunCapturedAsync(
                Arg.Is<ProcessSpec>(s => s.FileName == "docker"),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Doctor_ComposeProjectDockerMissing_RendersWarnAndExitCodeStaysZero()
    {
        var (_, processRunner, _, console, command) = CreateCommand();
        CreateComposeFile();
        StubDockerVersion(processRunner, 127, string.Empty);

        var exitCode = await command.RunAsync(
            new DoctorSettings { Project = _tempRoot },
            CancellationToken.None
        );

        Assert.Equal(0, exitCode);
        Assert.Contains("WARN", console.Output);
        Assert.Contains("Docker", console.Output);
    }

    // Bad -p handling (D4)

    [Fact]
    public async Task Doctor_ProjectPathDoesNotExist_MandatoryChecksStillRunWithoutCrashing()
    {
        var (_, _, _, console, command) = CreateCommand();
        var missingPath = Path.Combine(_tempRoot, "does-not-exist");

        var exitCode = await command.RunAsync(
            new DoctorSettings { Project = missingPath },
            CancellationToken.None
        );

        Assert.Equal(0, exitCode);
        Assert.Contains("PASS", console.Output);
        Assert.DoesNotContain("Docker", console.Output);
    }

    // Project resolution (spec: default and explicit -p)

    [Fact]
    public async Task Doctor_WithoutProjectOption_ResolvesCurrentDirectory()
    {
        var resolver = Substitute.For<IProjectContextResolver>();
        resolver
            .Resolve(Arg.Any<string>())
            .Returns(new ProjectContext(_tempRoot, string.Empty, Orchestrator.Plain, null, []));
        var (templatesRootLocator, processRunner, command) = CreateCommandWithResolver(resolver);
        templatesRootLocator.Resolve().Returns("/opt/dorn/templates");
        StubDotnetVersion(processRunner, 0, PassingSdkVersion);

        var originalDir = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(_tempRoot);
            // Directory.GetCurrentDirectory() may resolve through OS symlinks (e.g. macOS
            // /var -> /private/var), so re-read it instead of asserting against _tempRoot.
            var expectedCwd = Directory.GetCurrentDirectory();
            await command.RunAsync(new DoctorSettings { Project = null }, CancellationToken.None);

            resolver.Received(1).Resolve(expectedCwd);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDir);
        }
    }

    [Fact]
    public async Task Doctor_WithExplicitProjectOption_ResolvesThatPath()
    {
        var resolver = Substitute.For<IProjectContextResolver>();
        resolver
            .Resolve(Arg.Any<string>())
            .Returns(new ProjectContext(_tempRoot, string.Empty, Orchestrator.Plain, null, []));
        var (templatesRootLocator, processRunner, command) = CreateCommandWithResolver(resolver);
        templatesRootLocator.Resolve().Returns("/opt/dorn/templates");
        StubDotnetVersion(processRunner, 0, PassingSdkVersion);

        await command.RunAsync(new DoctorSettings { Project = _tempRoot }, CancellationToken.None);

        resolver.Received(1).Resolve(_tempRoot);
    }

    // Threat: subprocess argument composition

    [Fact]
    public async Task Doctor_MandatorySdkCheck_InvokesExactlyDotnetVersionArgs()
    {
        var (_, processRunner, _, _, command) = CreateCommand();
        StubDotnetVersion(processRunner, 0, PassingSdkVersion);

        await command.RunAsync(new DoctorSettings { Project = _tempRoot }, CancellationToken.None);

        await processRunner
            .Received(1)
            .RunCapturedAsync(
                Arg.Is<ProcessSpec>(s =>
                    s.FileName == "dotnet" && s.Arguments.SequenceEqual(new[] { "--version" })
                ),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Doctor_AdvisoryDockerCheck_InvokesExactlyDockerVersionArgs()
    {
        var (_, processRunner, _, _, command) = CreateCommand();
        CreateComposeFile();
        StubDockerVersion(processRunner, 0, "Docker version 27.0.0\n");

        await command.RunAsync(new DoctorSettings { Project = _tempRoot }, CancellationToken.None);

        await processRunner
            .Received(1)
            .RunCapturedAsync(
                Arg.Is<ProcessSpec>(s =>
                    s.FileName == "docker" && s.Arguments.SequenceEqual(new[] { "--version" })
                ),
                Arg.Any<CancellationToken>()
            );
    }

    // Healthy environment / exit fold

    [Fact]
    public async Task Doctor_AllMandatoryPassAndDockerWarn_ExitsZero()
    {
        var (templatesRootLocator, processRunner, _, console, command) = CreateCommand();
        templatesRootLocator.Resolve().Returns("/opt/dorn/templates");
        StubDotnetVersion(processRunner, 0, PassingSdkVersion);
        CreateComposeFile();
        StubDockerVersion(processRunner, 127, string.Empty);

        var exitCode = await command.RunAsync(
            new DoctorSettings { Project = _tempRoot },
            CancellationToken.None
        );

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("FAIL", console.Output);
    }

    [Fact]
    public async Task Doctor_ASingleMandatoryCheckFails_ExitsOne()
    {
        var (_, processRunner, _, _, command) = CreateCommand();
        StubDotnetVersion(processRunner, 0, "9.0.100\n");

        var exitCode = await command.RunAsync(
            new DoctorSettings { Project = _tempRoot },
            CancellationToken.None
        );

        Assert.Equal(1, exitCode);
    }

    // TryParseSdkVersion (internal, direct unit test)

    [Theory]
    [InlineData("10.0.301", true, "10.0.301")]
    [InlineData("10.0.100-preview.5.25277.114", true, "10.0.100")]
    [InlineData("10.0.301\n", true, "10.0.301")]
    [InlineData("", false, null)]
    [InlineData("not-a-version", false, null)]
    public void TryParseSdkVersion_VariousInputs_ParsesOrFailsAsExpected(
        string raw,
        bool expectedSuccess,
        string? expectedVersion
    )
    {
        var success = DoctorCommand.TryParseSdkVersion(raw, out var version);

        Assert.Equal(expectedSuccess, success);
        if (expectedSuccess)
        {
            Assert.Equal(Version.Parse(expectedVersion!), version);
        }
    }

    private void CreateComposeFile()
    {
        File.WriteAllText(Path.Combine(_tempRoot, "docker-compose.yml"), "services: {}");
    }

    private static void StubDotnetVersion(IProcessRunner processRunner, int exitCode, string stdout)
    {
        processRunner
            .RunCapturedAsync(
                Arg.Is<ProcessSpec>(s => s.FileName == "dotnet"),
                Arg.Any<CancellationToken>()
            )
            .Returns(new ProcessResult(exitCode, stdout, string.Empty));
    }

    private static void StubDockerVersion(IProcessRunner processRunner, int exitCode, string stdout)
    {
        processRunner
            .RunCapturedAsync(
                Arg.Is<ProcessSpec>(s => s.FileName == "docker"),
                Arg.Any<CancellationToken>()
            )
            .Returns(new ProcessResult(exitCode, stdout, string.Empty));
    }

    private (
        ITemplatesRootLocator TemplatesRootLocator,
        IProcessRunner ProcessRunner,
        IProjectContextResolver Resolver,
        TestConsole Console,
        DoctorCommand Command
    ) CreateCommand()
    {
        var templatesRootLocator = Substitute.For<ITemplatesRootLocator>();
        templatesRootLocator.Resolve().Returns("/opt/dorn/templates");

        var processRunner = Substitute.For<IProcessRunner>();
        StubDotnetVersion(processRunner, 0, PassingSdkVersion);

        var resolver = new ProjectContextResolver();
        var console = new TestConsole().Width(int.MaxValue);
        // Explicit — no test may rely on TestConsole's default Unicode/Interactive values.
        console.Profile.Capabilities.Unicode = true;
        console.Profile.Capabilities.Interactive = false;
        var theme = new DornTheme(console);

        var command = new DoctorCommand(
            templatesRootLocator,
            processRunner,
            resolver,
            console,
            theme
        );

        return (templatesRootLocator, processRunner, resolver, console, command);
    }

    private (
        ITemplatesRootLocator TemplatesRootLocator,
        IProcessRunner ProcessRunner,
        DoctorCommand Command
    ) CreateCommandWithResolver(IProjectContextResolver resolver)
    {
        var templatesRootLocator = Substitute.For<ITemplatesRootLocator>();
        var processRunner = Substitute.For<IProcessRunner>();
        var console = new TestConsole().Width(int.MaxValue);
        console.Profile.Capabilities.Unicode = true;
        console.Profile.Capabilities.Interactive = false;
        var theme = new DornTheme(console);

        var command = new DoctorCommand(
            templatesRootLocator,
            processRunner,
            resolver,
            console,
            theme
        );

        return (templatesRootLocator, processRunner, command);
    }
}
