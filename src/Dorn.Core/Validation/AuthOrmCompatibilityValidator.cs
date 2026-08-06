namespace Dorn.Core.Validation;

public static class AuthOrmCompatibilityValidator
{
    public static AuthValidationResult Validate(string auth, string orm)
    {
        if (
            string.Equals(auth, "custom", StringComparison.OrdinalIgnoreCase)
            && string.Equals(orm, "dapper", StringComparison.OrdinalIgnoreCase)
        )
        {
            return AuthValidationResult.Invalid(
                "Auth='custom' requires Orm='efcore' (Dapper has no schema for the seeded user)."
            );
        }

        return AuthValidationResult.Valid;
    }
}
