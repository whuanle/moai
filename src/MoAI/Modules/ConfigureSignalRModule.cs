using Maomi;
using Microsoft.AspNetCore.SignalR;
using MoAI.Infra;
using MoAI.Message.Hubs;
using StackExchange.Redis;

namespace MoAI.Modules;

/// <summary>
/// 配置 SignalR：事件推送 Hub 的宿主级装配，Redis backplane 支撑多实例共享连接状态
/// （任意实例发布的推送经 Redis 转发到用户连接所在实例）.
/// </summary>
public class ConfigureSignalRModule : IModule
{
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigureSignalRModule"/> class.
    /// </summary>
    /// <param name="systemOptions">系统配置.</param>
    public ConfigureSignalRModule(SystemOptions systemOptions)
    {
        _systemOptions = systemOptions!;
    }

    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        context.Services.AddSignalR()
            .AddStackExchangeRedis(_systemOptions.Redis, options =>
            {
                // 与平台其它 Redis key 一致使用 moai: 前缀，多套环境共用一个 Redis 时按前缀隔离
                options.Configuration.ChannelPrefix = RedisChannel.Literal("moai:sr");
            });

        // 连接归属用户（JWT sub），供 Clients.User(id) 定向推送与多端连接归组
        context.Services.AddSingleton<IUserIdProvider, MoAIUserIdProvider>();
    }
}
