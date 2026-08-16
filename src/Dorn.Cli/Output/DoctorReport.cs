namespace Dorn.Cli.Output;

public sealed record DoctorReport(IReadOnlyList<DoctorCheckDto> Checks);

/// <summary>Status is one of "pass", "fail", "warn" (lowercase, per the published contract).</summary>
public sealed record DoctorCheckDto(string Name, string Status, string Detail);
