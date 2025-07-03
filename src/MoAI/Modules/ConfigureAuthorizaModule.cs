// <copyright file="ConfigureAuthorizaModule.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

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
    private readonly IConfiguration _configuration;
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigureAuthorizaModule"/> class.
    /// </summary>
    /// <param name="configuration"></param>
    public ConfigureAuthorizaModule(IConfiguration configuration)
    {
        _configuration = configuration;
        _systemOptions = configuration.Get<SystemOptions>()!;
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