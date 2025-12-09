using FluentValidation;
using Maomi;
using MoAI.Infra;
using MoAI.Storage;
using System.Text.Json;

namespace MoAI.Modules;

/// <summary>
/// 配置 MVC .
/// </summary>
public class ConfigureMVCModule : IModule
{
    private readonly IConfiguration _configuration;
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigureMVCModule"/> class.
    /// </summary>
    /// <param name="configuration"></param>
    public ConfigureMVCModule(IConfiguration configuration)
    {
        _configuration = configuration;
        _systemOptions = configuration.Get<SystemOptions>()!;
    }

    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        var mvcBuilder = context.Services.AddControllers(o =>
        {
           // 序列化配置
           // o.Filters.Add<>();
        }).AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        });

        AddApplicationParts(mvcBuilder, context);

        context.Services.AddCors(options =>
        {
            options.AddPolicy(
                name: "AllowSpecificOrigins",
                policy =>
                              {
                                  policy.AllowAnyOrigin()
                                  .AllowAnyHeader()
                                  .AllowAnyMethod();
                              });
        });

        context.Services.AddValidatorsFromAssemblies(context.Modules.Select(x => x.Assembly).Distinct());
    }

    private static void AddApplicationParts(IMvcBuilder builder, ServiceContext context)
    {
        foreach (var item in context.Modules)
        {
            if (item.Assembly.GetName().Name?.EndsWith(".Api", StringComparison.CurrentCultureIgnoreCase) == true)
            {
                builder.AddApplicationPart(item.Assembly);
            }
        }

        builder.AddApplicationPart(typeof(StorageLocalModule).Assembly);
    }
}