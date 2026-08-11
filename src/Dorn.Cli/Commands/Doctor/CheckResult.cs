namespace Dorn.Cli.Commands.Doctor;

internal enum CheckStatus
{
    Pass,
    Fail,
    Warn,
}

internal sealed record CheckResult(string Name, CheckStatus Status, string Detail);
