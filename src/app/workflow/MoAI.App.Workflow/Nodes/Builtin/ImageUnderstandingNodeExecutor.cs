using System.Text.Json.Nodes;
using MoAI.App.Workflow.Definition;

namespace MoAI.App.Workflow.Nodes.Builtin;

/// <summary>
/// 图片理解节点执行器 - 通过 <see cref="IWorkflowVisionClient"/> 把图片交给视觉模型理解/提取内容.
/// config: { aiModelId（视觉模型 id，仅支持视觉的对话模型；与 AI 对话节点同键）, temperature（0-2，可空） }.
/// 输入：image（必填，http(s) 图片地址 / data: URI / 平台文件 objectKey）、prompt（必填，理解或提取指令）.
/// 输出：{ "answer": "..." }；流式输出片段通过进度事件推送.
/// </summary>
public class ImageUnderstandingNodeExecutor : INodeExecutor
{
    private readonly IWorkflowVisionClient _visionClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageUnderstandingNodeExecutor"/> class.
    /// </summary>
    public ImageUnderstandingNodeExecutor(IWorkflowVisionClient visionClient)
    {
        _visionClient = visionClient;
    }

    /// <inheritdoc/>
    public string NodeType => NodeTypes.ImageUnderstanding;

    /// <inheritdoc/>
    public async Task<NodeExecutionResult> ExecuteAsync(NodeExecutionContext context, CancellationToken cancellationToken)
    {
        if (!context.Inputs.TryGetPropertyValue("image", out var imageNode) || imageNode == null)
        {
            return NodeExecutionResult.Failure("图片理解节点缺少必需的输入字段：image");
        }

        var image = imageNode is JsonValue ? imageNode.GetValue<string>() : imageNode.ToJsonString();
        if (string.IsNullOrWhiteSpace(image))
        {
            return NodeExecutionResult.Failure("图片理解节点的输入 image 为空，请绑定图片地址");
        }

        if (!context.Inputs.TryGetPropertyValue("prompt", out var promptNode) || promptNode == null)
        {
            return NodeExecutionResult.Failure("图片理解节点缺少必需的输入字段：prompt");
        }

        var prompt = promptNode is JsonValue ? promptNode.GetValue<string>() : promptNode.ToJsonString();
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return NodeExecutionResult.Failure("图片理解节点的输入 prompt 为空，请填写理解或提取指令");
        }

        var model = context.GetConfigString("aiModelId");

        // 温度：0-2 有效，越界视为未设置（用渠道默认）
        var temperature = context.GetConfigFloat("temperature");
        if (temperature is < 0 or > 2)
        {
            temperature = null;
        }

        try
        {
            var answer = await _visionClient.UnderstandAsync(
                new VisionRequest
                {
                    Prompt = prompt,
                    Image = image,
                    Model = model,
                    Temperature = temperature,
                },
                async chunk => await context.ReportProgressAsync(chunk, cancellationToken),
                cancellationToken);

            return NodeExecutionResult.Success(new JsonObject
            {
                ["answer"] = answer,
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 仅外部真实取消才上抛；内部超时（TaskCanceled 等）走下方通用失败分支，避免 500/实例卡死
            throw;
        }
        catch (Exception ex)
        {
            return NodeExecutionResult.Failure($"图片理解执行失败：{ex.Message}");
        }
    }
}
