using Maomi;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MoAI.AI.Services;
using MoAI.Feishu.Services;

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

        // 流程执行过程内容（DataContent，媒体类型标记）→ AG-UI CustomEvent；
        // 仅流程应用对话产生该内容类型，Agent 应用流不受影响
        context.Services.Configure<AGUI.Server.AGUIStreamOptions>(options =>
        {
            options.MapContent(content => content is Microsoft.Extensions.AI.DataContent data
                    && data.MediaType == MoAI.AI.Services.WorkflowChatStreamContract.DataMediaType
                ? new[]
                {
                    new AGUI.Abstractions.CustomEvent
                    {
                        Name = MoAI.AI.Services.WorkflowChatStreamContract.EventName,
                        Value = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(data.Data.Span),
                    },
                }
                : null);
        });

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

        // 应用渠道的飞书消息处理器（群聊/私聊消息 → 应用 Agent → 回复）；单例：内部按 chat_id 加锁串行
        context.Services.AddSingleton<IFeishuEventHandler, AppFeishuMessageHandler>();
    }
}
