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
    }
}
