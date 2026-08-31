using Maomi;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MoAI.Infra;
using MoAI.Infra.Services;
using System.Security.Claims;

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

                options.Events = new JwtBearerEvents
                {
                    OnChallenge = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ConfigureAuthorizaModule>>();
                        logger.LogWarning("JWT OnChallenge 触发：[Authorize] 被拒绝。Authorization头部存在={HasToken}，原因={Reason}",
                            context.Request.Headers.Authorization.ToString(), context.AuthenticateFailure?.Message ?? context.Error);
                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        var logger = context.HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ConfigureAuthorizaModule>>();
                        logger.LogError(context.Exception, "JWT 认证失败，token 无法通过验证: {Message} / [NoToken] = {NoToken}",
                            context.Exception?.Message, context.Request.Headers.Authorization.Count == 0 ? "true(未携带Authorization头)" : "false");
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        var logger = context.HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ConfigureAuthorizaModule>>();
                        var principal = context.Principal;
                        logger.LogInformation("JWT TokenValidated 成功. IsAuthenticated={IsAuth}, Claims={Claims}, NameIdentifier={NameId}",
                            principal?.Identity?.IsAuthenticated,
                            string.Join(",", principal?.Claims.Select(c => $"{c.Type}={c.Value}") ?? Enumerable.Empty<string>()),
                            principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value);
                        return Task.CompletedTask;
                    },
                };
            });

        context.Services.AddAuthorization();
    }
}