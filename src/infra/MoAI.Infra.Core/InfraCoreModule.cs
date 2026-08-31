using FluentValidation;
using Maomi;
using Maomi.MQ;
using Maomi.MQ.Filters;
using Maomi.MQ.Models;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MoAI.Infra.Defaults;
using MoAI.Infra.Models;
using MoAI.Infra.Service;
using RabbitMQ.Client;

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

    /// <inheritdoc/>
    public override void ConfigureServices(ServiceContext context)
    {
        var systemOptions = _configurationManager.GetSection("MoAI").Get<SystemOptions>() ?? throw new FormatException("The system configuration cannot be loaded.");

        context.Services.AddSingleton<Services.IIdProvider>(new DefaultIdProvider((ushort)0));
        context.Services.AddHttpContextAccessor();

        context.Services.AddSingleton<IAESProvider>(s => { return new AESProvider(systemOptions.AES); });

        context.Services.AddMaomiMQ(
            (MqOptionsBuilder options) =>
            {
                options.WorkId = 1;
                options.AutoQueueDeclare = true;
                options.AppName = systemOptions.Name;
                options.Rabbit = (ConnectionFactory options) =>
                {
                    options.Uri = new Uri(systemOptions.RabbitMQ!);
                    options.ConsumerDispatchConcurrency = 100;
                    options.ClientProvidedName = "moai";
                };
            },
            context.Modules.Select(x => x.Assembly).ToArray(),
            [new ConsumerTypeFilter(), new EventBusTypeFilter()]);
    }

    /// <inheritdoc/>
    public override void TypeFilter(ServiceContext context, Type type)
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

            context.Services.AddScoped(typeof(IValidator<>).MakeGenericType(type), typeof(AutoValidator<>).MakeGenericType(type));
            context.Services.AddScoped(type);
        }
    }
}
