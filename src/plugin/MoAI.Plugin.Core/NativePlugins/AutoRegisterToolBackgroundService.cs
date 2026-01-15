using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MoAI.AI.Models;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Database.Helper;
using MoAI.Infra.Extensions;
using MoAI.Plugin;

namespace MoAI.Hangfire;

/// <summary>
/// 注册定时任务
/// </summary>
internal class AutoRegisterToolBackgroundService : BackgroundService
{
    private readonly ILogger<AutoRegisterToolBackgroundService> _logger;
    private readonly IHostApplicationLifetime _hostApplicationLifetime;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoRegisterToolBackgroundService"/> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="hostApplicationLifetime"></param>
    /// <param name="serviceScopeFactory"></param>
    public AutoRegisterToolBackgroundService(ILogger<AutoRegisterToolBackgroundService> logger, IHostApplicationLifetime hostApplicationLifetime, IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _hostApplicationLifetime = hostApplicationLifetime;
        _serviceScopeFactory = serviceScopeFactory;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var applicationStartedTask = WaitForHostStartedAsync(stoppingToken);
        await applicationStartedTask;

        // web 取消启动
        if (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        using var socpe = _serviceScopeFactory.CreateScope();
        var nativePluginFactory = socpe.ServiceProvider.GetRequiredService<INativePluginFactory>();
        var databaseContext = socpe.ServiceProvider.GetRequiredService<DatabaseContext>();

        var pluginTemplates = nativePluginFactory.GetPlugins();
        var pluginTemplatesQuery = pluginTemplates.AsQueryable().Where(x => x.PluginType == PluginType.ToolPlugin);

        // 检测数据库没对应的插件时自动注册
        var toolPlugins = databaseContext.Plugins.Where(x => x.Type == (int)PluginType.ToolPlugin);
        List<PluginNativeEntity> pluginNativeEntities = new();
        List<PluginEntity> pluginEntities = new();

        var existingPluginNames = toolPlugins.Select(x => x.PluginName).ToHashSet();

        foreach (var pluginTemplate in pluginTemplatesQuery)
        {
            if (!existingPluginNames.Contains(pluginTemplate.Key))
            {
                var nativePlugin = new PluginNativeEntity
                {
                    Config = "{}",
                    TemplatePluginClassify = pluginTemplate.Classify.ToJsonString(),
                    TemplatePluginKey = pluginTemplate.Key,
                };
                pluginNativeEntities.Add(nativePlugin);
            }
        }

        if (pluginNativeEntities.Count <= 0)
        {
            return;
        }

        using var tran = TransactionScopeHelper.Create();
        databaseContext.PluginNatives.AddRange(pluginNativeEntities);
        await databaseContext.SaveChangesAsync(stoppingToken);

        foreach (var nativePlugin in pluginNativeEntities)
        {
            var template = pluginTemplatesQuery.First(x => x.Key == nativePlugin.TemplatePluginKey);
            var newPlugin = new PluginEntity
            {
                PluginName = template.Key,
                Title = template.Name,
                Description = template.Description,
                ClassifyId = 0,
                Counter = 0,
                IsPublic = true,
                PluginId = nativePlugin.Id, // 使用保存后生成的 Id
                TeamId = 0,
                Type = (int)PluginType.ToolPlugin,
            };
            pluginEntities.Add(newPlugin);
            _logger.LogInformation("Auto registered tool plugin: {PluginName} ({PluginIdentifier})", newPlugin.Title, newPlugin.PluginName);
        }

        databaseContext.Plugins.AddRange(pluginEntities);
        await databaseContext.SaveChangesAsync(stoppingToken);

        tran.Complete();
    }

    // 避免阻塞 Web 启动，在 ASP.NET Core 启动完毕后才会启动此后台服务
    private Task<bool> WaitForHostStartedAsync(CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>();

        _hostApplicationLifetime.ApplicationStarted.Register(() =>
        {
            tcs.TrySetResult(true);
        });

        cancellationToken.Register(() =>
        {
            tcs.TrySetCanceled();
        });

        return tcs.Task;
    }
}