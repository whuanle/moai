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

        // 应用使用次数计数器：Redis 自增 + 统计模块每分钟批量累加到 app.use_count
        context.Services.AddScoped<IAppUsageCounter, AppUsageCounter>();
        context.Services.AddScoped<MoAI.Statistics.Services.IResourceUsageWriter, AppUsageCounterActivatorJob>();

        // 应用消息条数计数器：会话落库时推送增量 + 统计模块每分钟批量累加到 app.message_count
        context.Services.AddScoped<IAppMessageUsageCounter, AppMessageUsageCounter>();
        context.Services.AddScoped<MoAI.Statistics.Services.IResourceUsageWriter, AppMessageUsageCounterActivatorJob>();

        // 使用分析聚合缓存：tokens/调用次数/按模型分布 1 分钟 cache-aside
        context.Services.AddScoped<AppUsageAggregateCache>();
    }
}
