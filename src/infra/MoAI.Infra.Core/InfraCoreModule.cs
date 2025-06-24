// <copyright file="InfraCoreModule.cs" company="MaomiAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Maomi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoAI.Infra.Defaults;
using MoAI.Infra.Service;
using MoAI.Infra.Services;
using System.Security.Cryptography;

namespace MoAI.Infra;

/// <summary>
/// InfraCoreModule.
/// </summary>
[InjectModule<InfraConfigurationModule>]
public class InfraCoreModule : IModule
{
    private readonly IConfigurationManager _configurationManager;
    private readonly ILogger<InfraCoreModule> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InfraCoreModule"/> class.
    /// </summary>
    /// <param name="configurationManager"></param>
    /// <param name="logger"></param>
    public InfraCoreModule(IConfigurationManager configurationManager, ILogger<InfraCoreModule> logger)
    {
        _configurationManager = configurationManager;
        _logger = logger;
    }

    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        var systemOptions = _configurationManager.Get<SystemOptions>() ?? throw new FormatException("The system configuration cannot be loaded.");

        context.Services.AddSingleton<IIdProvider>(new DefaultIdProvider(0));
        context.Services.AddHttpContextAccessor();

        context.Services.AddSingleton<IAESProvider>(s => { return new AESProvider(systemOptions.AES); });
        ConfigureRsaPrivate(context);
    }

    // 生成 RSA 私钥
    private void ConfigureRsaPrivate(ServiceContext context)
    {
        if (!File.Exists(AppConst.PrivateRSA))
        {
            using RSA? rsa = RSA.Create(2048);
            string rsaPrivate = rsa.ExportPkcs8PrivateKeyPem();
            File.WriteAllText(AppConst.PrivateRSA, rsaPrivate);
            context.Services.AddSingleton<IRsaProvider>(s => { return new RsaProvider(rsaPrivate); });
        }
        else
        {
            string? rsaPrivate = File.ReadAllText(Path.Combine(AppConst.AppPath, AppConst.PrivateRSA));
            context.Services.AddSingleton<IRsaProvider>(s => { return new RsaProvider(rsaPrivate); });
        }

        _logger.LogCritical("RSA private key file: {RsaPrivateKeyPath}", AppConst.PrivateRSA);
    }
}