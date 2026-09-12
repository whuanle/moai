using Maomi;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MoAI.AI.Services;

namespace MoAI.AI;

/// <summary>
/// AiCoreModule.
/// </summary>
[InjectModule<AiSharedModule>]
public class AiCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        // AG-UI 服务端（JSON 选项/SSE 管线）
        context.Services.AddAGUIServer();

        // 动态派发 Agent：单实例，按请求路由 appId + 用户 + 会话装配真正的应用 Agent
        context.Services.AddKeyedSingleton<AIAgent>(AppAgentConstants.AgentName, (sp, _) =>
        {
            var loggerFactory = sp.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>();
            var shell = new ChatClientAgent(
                new NoopChatClient(),
                new ChatClientAgentOptions { Name = AppAgentConstants.AgentName },
                loggerFactory);

            return new AppAgentDispatcher(shell, sp.GetRequiredService<IServiceScopeFactory>());
        });

        // 会话存储：热态 Redis + 完成后落库（与 Agent 名称同 key）
        context.Services.AddKeyedSingleton<AgentSessionStore>(AppAgentConstants.AgentName, (sp, _) =>
            new AppAgentSessionStore(sp.GetRequiredService<IServiceScopeFactory>()));

        // 沙箱回收定时任务（依赖 Hangfire，若未注册 IRecurringJobManager 则该服务不生效）
        context.Services.AddHostedService<MoAI.AI.Services.SandboxReaperRegistrationService>();
    }
}
