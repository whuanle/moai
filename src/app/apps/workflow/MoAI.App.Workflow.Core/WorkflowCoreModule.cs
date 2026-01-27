using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Workflow.Runtime;
using MoAI.Workflow.Services;

namespace MoAI.App.Workflow;

/// <summary>
/// Workflow Core 模块.
/// </summary>
[InjectModule<WorkflowSharedModule>]
public class WorkflowCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        // 注册变量解析服务
        context.Services.AddScoped<VariableResolutionService>();

        // 注册表达式评估服务
        context.Services.AddScoped<ExpressionEvaluationService>();

        // 注册工作流定义服务
        context.Services.AddScoped<WorkflowDefinitionService>();

        // 注册工作流上下文管理器
        context.Services.AddScoped<WorkflowContextManager>();

        // 注册工作流运行时
        context.Services.AddScoped<WorkflowRuntime>();

        // 注册节点运行时
        context.Services.AddScoped<INodeRuntime, StartNodeRuntime>();
        context.Services.AddScoped<INodeRuntime, EndNodeRuntime>();
        context.Services.AddScoped<INodeRuntime, AiChatNodeRuntime>();
        context.Services.AddScoped<INodeRuntime, WikiNodeRuntime>();
        context.Services.AddScoped<INodeRuntime, PluginNodeRuntime>();
        context.Services.AddScoped<INodeRuntime, ConditionNodeRuntime>();
        context.Services.AddScoped<INodeRuntime, ForEachNodeRuntime>();
        context.Services.AddScoped<INodeRuntime, ForkNodeRuntime>();
        context.Services.AddScoped<INodeRuntime, JavaScriptNodeRuntime>();
        context.Services.AddScoped<INodeRuntime, DataProcessNodeRuntime>();

        // 注册节点运行时工厂
        context.Services.AddScoped<NodeRuntimeFactory>();
    }
}
