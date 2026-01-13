using Hangfire;
using Hangfire.Redis.StackExchange;
using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Hangfire.Services;
using MoAI.Infra;

namespace MoAI.Hangfire;

/// <summary>
/// HangfireCoreModule.
/// </summary>
[InjectModule<HangfireSharedModule>]
public class HangfireCoreModule : IModule
{
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="HangfireCoreModule"/> class.
    /// </summary>
    /// <param name="systemOptions"></param>
    public HangfireCoreModule(SystemOptions systemOptions)
    {
        _systemOptions = systemOptions;
    }

    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        var options =
            new RedisStorageOptions
            {
                Prefix = "moai:hangfire"
            };

        context.Services.AddHangfire(
            config =>
            {
                config.UseRedisStorage(_systemOptions.Redis, options)
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings();
                config.UseActivator(new HangfireActivator(context.Services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>()));
            });

        context.Services.AddHangfireServer(options =>
        {
            // 默认 10 查一次任务
            options.SchedulePollingInterval = TimeSpan.FromSeconds(10);

            // 工作者数量
            options.WorkerCount = Environment.ProcessorCount * 2;
        });

        context.Services.AddHostedService<AutoRegisterHangfireBackgroundService>();
    }
}
