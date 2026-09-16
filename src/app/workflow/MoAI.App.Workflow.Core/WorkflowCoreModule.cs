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
    }
}
