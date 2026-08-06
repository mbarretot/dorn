#if (UseAuth)
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CleanArchWebApi.WebApi.Extensions;

public static class AuthenticationExtensions
{
#if (UseCustomAuth)
    public static IServiceCollection AddCustomJwtAuth(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment
    )
    {
        var signingKey = configuration["Jwt:SigningKey"];
        if (
            string.IsNullOrWhiteSpace(signingKey)
            || signingKey.StartsWith("REPLACE", StringComparison.Ordinal)
        )
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "Jwt:SigningKey is missing or a placeholder outside the Development environment. "
                        + "Configure a real signing key via 'dotnet user-secrets' (id: a51cf652-fca9-43cd-b972-18af75625fa1) or an environment variable before deploying."
                );
            }
        }

        return services;
    }
#endif

#if (UseAzureAdAuth)
    public static IServiceCollection AddAzureAdAuth(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, _ => { });

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme);
        services.PostConfigure<JwtBearerOptions>(
            JwtBearerDefaults.AuthenticationScheme,
            options =>
            {
                options.Authority =
                    configuration["AzureAd:Instance"] + configuration["AzureAd:TenantId"] + "/v2.0";
                options.MetadataAddress = options.Authority + "/.well-known/openid-configuration";
            }
        );

        return services;
    }
#endif
}
#endif
