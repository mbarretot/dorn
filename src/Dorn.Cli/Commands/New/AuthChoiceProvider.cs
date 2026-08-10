using Dorn.Core.Validation;

namespace Dorn.Cli.Commands.New;

public static class AuthChoiceProvider
{
    private static readonly string[] AllChoices = ["none", "custom", "azure-ad"];

    public static IReadOnlyList<string> ForOrm(string orm)
    {
        var compatible = AllChoices
            .Where(auth => AuthOrmCompatibilityValidator.Validate(auth, orm).IsValid)
            .ToArray();

        return compatible.Length > 0 ? compatible : AllChoices;
    }
}
