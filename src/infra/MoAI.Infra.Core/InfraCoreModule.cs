using Maomi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoAI.Infra.Defaults;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.Infra;

/// <summary>
/// InfraCoreModule.
/// </summary>
[InjectModule<InfraConfigurationModule>]
[InjectModule<InfraExternalHttpModule>]
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
        var systemOptions = _configurationManager.GetSection("MoAI").Get<SystemOptions>() ?? throw new FormatException("The system configuration cannot be loaded.");

        context.Services.AddSingleton<IIdProvider>(new DefaultIdProvider(0));
        context.Services.AddHttpContextAccessor();

        // context.Services.AddSingleton<IAESProvider>(s => { return new AESProvider(systemOptions.AES); });

        // 注册默认服务，会被上层模块覆盖
        context.Services.AddScoped<UserContext, DefaultUserContext>();
    }
}