using Maomi;
using Microsoft.Extensions.Options;
using MoAI.Infra;
using MoAI.Login.Services;
using Serilog;

namespace MoAI;

/// <summary>
/// MainExtensions.
/// </summary>
public static partial class MainExtensions
{
    /// <summary>
    /// UseMoAI.
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static IHostApplicationBuilder UseMoAI(this IHostApplicationBuilder builder)
    {
        if (!Directory.Exists(AppConst.ConfigsPath))
        {
            InitConfigurationDirectory();
        }

        ImportSystemConfiguration(builder);

        var systemOptions = builder.Configuration.GetSection("MoAI").Get<SystemOptions>() ?? throw new FormatException("The system configuration cannot be loaded.");

        builder.Services.AddSingleton(systemOptions);

        builder.Services.AddSingleton<IConfigurationManager>(builder.Configuration);

        builder.Logging.ClearProviders();
        builder.Services.AddSerilog((services, configuration) =>
        {
            configuration.ReadFrom.Services(services);
            configuration.ReadFrom.Configuration(builder.Configuration);
        });

        builder.Services.AddModule<MainModule>();

        return builder;
    }

    /// <summary>
    /// UseMoAI.
    /// </summary>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static IApplicationBuilder UseMoAI(this IApplicationBuilder builder)
    {
        // 使用认证中间件
        builder.UseMiddleware<CustomAuthorizaMiddleware>();

        // builder.UseLocalFiles();
        return builder;
    }
}