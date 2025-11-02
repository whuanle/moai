using Maomi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoAI.Infra.Service;
using MoAI.Infra.Services;
using System.Security.Cryptography;

namespace MoAI.Infra;

/// <summary>
/// InfraConfigurationModule.
/// </summary>
public class InfraConfigurationModule : IModule
{
    private readonly ILogger _logger;
    private readonly IConfigurationManager _configurationManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="InfraConfigurationModule"/> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="configurationManager"></param>
    public InfraConfigurationModule(ILogger<InfraConfigurationModule> logger, IConfigurationManager configurationManager)
    {
        _logger = logger;
        _configurationManager = configurationManager;
    }

    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        ConfigureRsaPrivate(context);
    }

    // 生成 RSA 私钥
    private static void ConfigureRsaPrivate(ServiceContext context)
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
    }
}