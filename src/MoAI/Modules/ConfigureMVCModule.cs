using FluentValidation;
using Maomi;
using MoAI.Infra;

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
        context.Services.AddControllers(o =>
        {

        });

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

        //context.Services.AddFluentValidationAutoValidation();
    }
}