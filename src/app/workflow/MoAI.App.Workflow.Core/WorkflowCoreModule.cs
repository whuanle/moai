using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.App.Workflow.Instance;

namespace MoAI.App.Workflow;

/// <summary>
/// WorkflowCoreModule.
/// </summary>
[InjectModule<WorkflowSharedModule>]
[InjectModule<WorkflowApiModule>]
public class WorkflowCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        // 工作流引擎（编译/调度/内置节点执行器），存储与端口实现由本模块的 [InjectOnScoped] 服务提供
        context.Services.AddMoAIWorkflow();

        // 节点数据净化：按应用安全策略对节点输入/输出/错误消息脱敏（引擎检查点/事件/下游引用生效）
        context.Services.AddScoped<INodeDataSanitizer, Services.WorkflowNodeSecuritySanitizer>();
    }
}
