#if (UseAzureAdAuth)
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace CleanArchWebApi.Functional.Tests.Auth;

/// <summary>
/// AddAzureAdAuth wires JwtBearerOptions via its own PostConfigure (D3). This factory only fakes
/// the parts that need a live Entra ID to work (Authority/MetadataAddress discovery, signing key) —
/// it registers its own PostConfigure after Program.cs's own configuration runs, so it applies last
/// and overrides just those two things (T4: no live Entra ID network dependency in CI). It
/// deliberately does NOT touch ValidateAudience/ValidAudiences: AzureAd:ClientId is supplied via
/// UseSetting instead, so AddAzureAdAuth's own audience-derivation logic runs for real and is
/// actually exercised by these tests, not bypassed.
/// </summary>
public sealed class AzureAdWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string SigningKey = "azure-ad-test-signing-key-32-bytes-1234";
    public const string Issuer = "https://test-issuer.example.com";
    public const string Audience = "test-api";

    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(),
        $"{Guid.NewGuid()}.db"
    );

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder
            .UseSetting("AzureAd:ClientId", Audience)
            .UseSetting("AzureAd:Instance", "https://unused.example.com/")
            .UseSetting("AzureAd:TenantId", "unused-tenant");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();

            services.AddDbContext<ApplicationDbContext>(options =>
                options
                    .UseSqlite($"Data Source={_databasePath}")
                    .ConfigureWarnings(warnings =>
                        warnings.Ignore(RelationalEventId.PendingModelChangesWarning)
                    )
            );

            services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme,
                options =>
                {
                    options.Authority = null;
                    options.MetadataAddress = string.Empty;
                    options.ConfigurationManager =
                        new StaticConfigurationManager<OpenIdConnectConfiguration>(
                            new OpenIdConnectConfiguration()
                        );
                    options.TokenValidationParameters.ValidateIssuer = true;
                    options.TokenValidationParameters.ValidIssuer = Issuer;
                    options.TokenValidationParameters.ValidateLifetime = true;
                    options.TokenValidationParameters.ValidateIssuerSigningKey = true;
                    options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(SigningKey)
                    );
                    options.TokenValidationParameters.ClockSkew = TimeSpan.FromMinutes(1);
                }
            );
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.Migrate();
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        if (File.Exists(_databasePath))
        {
            File.Delete(_databasePath);
        }
    }
}
#endif
