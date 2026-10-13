using System.Text.Json.Nodes;
using Maomi;
using MoAI.AI.Services;
using MoAI.App.Workflow.Nodes;

namespace MoAI.App.Workflow.Services;

/// <summary>
/// 工作流图片理解端口实现：imageUnderstanding 节点的引擎端口适配器——
/// 只做引擎契约（<see cref="VisionRequest"/>）到 AI 模块统一入口（<see cref="IWorkflowNodeAiInvoker.VisionAsync"/>）的映射；
/// 视觉模型校验、图片来源解析（URL 直传/objectKey 读字节内联）、多模态消息组装全部由 AI 模块统一实现，本类不接入模型.
/// </summary>
[InjectOnScoped]
public class WorkflowVisionClient : IWorkflowVisionClient
{
    private readonly IWorkflowNodeAiInvoker _aiInvoker;
    private readonly WorkflowExecutionContext _executionContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowVisionClient"/> class.
    /// </summary>
    /// <param name="aiInvoker">AI 模块统一模型接入入口.</param>
    /// <param name="executionContext">工作流执行上下文（同作用域）.</param>
    public WorkflowVisionClient(IWorkflowNodeAiInvoker aiInvoker, WorkflowExecutionContext executionContext)
    {
        _aiInvoker = aiInvoker;
        _executionContext = executionContext;
    }

    /// <inheritdoc/>
    public Task<string> UnderstandAsync(VisionRequest request, Func<string, Task>? onProgress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Model) || !Guid.TryParse(request.Model, out var modelId))
        {
            throw new WorkflowException("图片理解节点未配置视觉模型，请在节点配置的「视觉模型」中选择");
        }

        return _aiInvoker.VisionAsync(new WorkflowNodeVisionRequest
        {
            ModelId = modelId,
            TeamId = (int)_executionContext.TeamId,
            AppId = _executionContext.AppId,
            UserId = _executionContext.UserId,
            Prompt = request.Prompt,
            Image = request.Image,
            Temperature = request.Temperature,
        }, onProgress, cancellationToken);
    }
}
