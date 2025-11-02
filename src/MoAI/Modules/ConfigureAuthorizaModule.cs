using Maomi;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MoAI.Infra;
using MoAI.Infra.Services;

namespace MoAI.Modules;

/// <summary>
/// 配置 授权 .
/// </summary>
public class ConfigureAuthorizaModule : IModule
{
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigureAuthorizaModule"/> class.
    /// </summary>
    /// <param name="systemOptions"></param>
    public ConfigureAuthorizaModule(SystemOptions systemOptions)
    {
        _systemOptions = systemOptions!;
    }

    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        using var serviceProvider = context.Services.BuildServiceProvider();
        var rsaProvider = serviceProvider.GetRequiredService<IRsaProvider>();

#if DEBUG
        Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
#endif

        context.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    RequireExpirationTime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = rsaProvider.GetRsaSecurityKey(),
                    ValidateIssuer = true,
                    ValidIssuer = _systemOptions.Server,
                    ValidateAudience = true,
                    ValidAudience = _systemOptions.Server,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                };
            });

        context.Services.AddAuthorization();
    }
}