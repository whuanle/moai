using FluentValidation;
using Maomi;
using MediatR;
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
public class InfraCoreModule : ModuleCore
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

    private ServiceContext _serviceContext;

    /// <inheritdoc/>
    public override void ConfigureServices(ServiceContext context)
    {
        _serviceContext = context;
        var systemOptions = _configurationManager.GetSection("MoAI").Get<SystemOptions>() ?? throw new FormatException("The system configuration cannot be loaded.");

        context.Services.AddSingleton<IIdProvider>(new DefaultIdProvider(0));
        context.Services.AddHttpContextAccessor();

        // context.Services.AddSingleton<IAESProvider>(s => { return new AESProvider(systemOptions.AES); });

        // 注册默认服务，会被上层模块覆盖
        context.Services.AddScoped<UserContext, DefaultUserContext>();
    }

    /// <inheritdoc/>
    public override void TypeFilter(Type type)
    {
        if (type.IsClass)
        {
            var validator = type.GetInterfaces().Where(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IModelValidator<>)).FirstOrDefault();
            if (validator == null)
            {
                return;
            }

            // 避免继承
            if (validator.GenericTypeArguments[0] != type)
            {
                return;
            }

            _serviceContext.Services.AddScoped(typeof(IValidator<>).MakeGenericType(type), typeof(AutoValidator<>).MakeGenericType(type));
            _serviceContext.Services.AddScoped(type);
        }
    }
}
