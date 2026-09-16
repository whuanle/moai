using Microsoft.Extensions.DependencyInjection;
using MoAI.App.Workflow.DataTransfer;
using MoAI.App.Workflow.Definition;
using MoAI.App.Workflow.Events;
using MoAI.App.Workflow.Instance;
using MoAI.App.Workflow.Nodes;
using MoAI.App.Workflow.Nodes.Builtin;

namespace MoAI.App.Workflow;

/// <summary>
/// 工作流引擎依赖注入配置.
/// </summary>
public static class WorkflowServiceCollectionExtensions
{
    /// <summary>
    /// 注册工作流引擎（流程定义 + 流程实例 + 流程数据传输 + 内置节点）.
    /// 存储实现（<see cref="Persistence.IWorkflowDefinitionStore"/>/<see cref="Persistence.IWorkflowInstanceStore"/>/<see cref="Persistence.IWorkflowEventLogStore"/>）
    /// 与端口实现（<see cref="Nodes.IAiChatClient"/>/<see cref="Nodes.IWorkflowPluginInvoker"/>）由宿主自行注册.
    /// 引擎对象图整体为 Scoped：调度器执行期间依赖实例存储做检查点落库，插件/AI 端口通常也依赖 Scoped 服务.
    /// </summary>
    public static IServiceCollection AddMoAIWorkflow(this IServiceCollection services)
    {
        // 流程定义（无状态，单例）
        services.AddSingleton<WorkflowValidator>();
        services.AddSingleton<WorkflowCompiler>();

        // 流程数据传输（无状态，单例）
        services.AddSingleton<IExpressionEvaluator, ExpressionEvaluator>();
        services.AddSingleton<InputResolver>();

        // 执行观察：事件推送（Scoped，与引擎同生命周期，避免向单例发布器订阅泄漏）
        services.AddScoped<IWorkflowEventPublisher, WorkflowEventPublisher>();

        // 流程实例
        services.AddScoped<WorkflowScheduler>();
        services.AddScoped<WorkflowEngine>();

        // 节点执行器：内置节点 + 注册表收集（注册表跟随 Scoped，才能持有当前作用域的端口实现）
        services.AddScoped<INodeExecutor, StartNodeExecutor>();
        services.AddScoped<INodeExecutor, EndNodeExecutor>();
        services.AddScoped<INodeExecutor, ConditionNodeExecutor>();
        services.AddScoped<INodeExecutor>(sp => new PluginNodeExecutor(sp.GetRequiredService<IWorkflowPluginInvoker>()));
        services.AddScoped<INodeExecutor>(sp => new AiChatNodeExecutor(sp.GetRequiredService<IAiChatClient>()));
        services.AddScoped<INodeExecutor, JavaScriptNodeExecutor>();
        services.AddScoped<INodeExecutorRegistry>(sp =>
        {
            var registry = new NodeExecutorRegistry();
            foreach (var executor in sp.GetServices<INodeExecutor>())
            {
                registry.Register(executor);
            }

            return registry;
        });

        return services;
    }
}
