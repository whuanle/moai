using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.App.Services;
using MoAI.Storage;

namespace MoAI.App;

/// <summary>
/// AppCoreModule.
/// </summary>
[InjectModule<AppSharedModule>]
[InjectModule<AppApiModule>]
[InjectModule<StorageSharedModule>]
public class AppCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        // 对话附件文本提取（Maomi.ToMarkdown 进程内库）
        context.Services.AddTextExtraction();

        // 应用使用次数计数器：Redis 自增 + 每分钟批量累加到 app.use_count
        context.Services.AddScoped<IAppUsageCounter, AppUsageCounter>();
        context.Services.AddScoped<MoAI.Hangfire.Services.ICounterActivatorJob, AppUsageCounterActivatorJob>();    }
}
