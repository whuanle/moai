using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Hangfire;
using MoAI.Statistics.Services;

namespace MoAI.Statistics;

/// <summary>
/// StatisticsCoreModule.
/// </summary>
[InjectModule<StatisticsSharedModule>]
[InjectModule<StatisticsApiModule>]
[InjectModule<HangfireCoreModule>]
public class StatisticsCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        // 资源使用统计领域服务（业务模块统一入口：聚合后批量原子增/减 Redis）
        context.Services.AddScoped<IResourceUsageService, ResourceUsageService>();

        // 对话轮次收集器（scoped，作用域结束自动批量 flush），agent 一轮对话内多次工具调用只推一次
        context.Services.AddScoped<IResourceUsageCollector, ResourceUsageCollector>();

        // 统一统计表（resource_usage_daily / resource_usage_total）落库服务
        context.Services.AddScoped<ResourceUsageStore>();

        // 注册每分钟批量落库任务（Redis counter -> 写入器 + 统一统计表）
        context.Services.AddHostedService<AutoRegisterStatisticsBackgroundService>();
    }
}
