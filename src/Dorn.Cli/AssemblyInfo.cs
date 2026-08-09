using System.Runtime.CompilerServices;

// Grants Dorn.Cli.Tests direct access to internal members (e.g. DoctorCommand.TryParseSdkVersion)
// so version-parsing edge cases can be unit tested without going through the process boundary.
[assembly: InternalsVisibleTo("Dorn.Cli.Tests")]
