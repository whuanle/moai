using System.Text.Json.Nodes;
using Maomi;
using MediatR;
using MoAI.AIPlugin.Commands;
using MoAI.AIPlugin.Models;
using MoAI.App.Workflow.Nodes;

namespace MoAI.App.Workflow.Services;

/// <summary>
/// 工作流插件调用端口实现：plugin 节点通过统一插件执行入口 <see cref="RunPluginCommand"/>
/// 按 key 执行静态/动态插件. 节点 config.pluginKey 为插件标识，config.configJson 可选（动态插件配置）.
/// </summary>
[InjectOnScoped]
public class WorkflowPluginInvoker : IWorkflowPluginInvoker
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowPluginInvoker"/> class.
    /// </summary>
    /// <param name="mediator">MediatR 实例.</param>
    public WorkflowPluginInvoker(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<JsonObject> InvokeAsync(string pluginKey, JsonObject parameters, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RunPluginCommand
        {
            Key = pluginKey,
            RequestJson = parameters.ToJsonString(),
        }, cancellationToken);

        if (!result.Success)
        {
            throw new WorkflowException($"插件 {pluginKey} 执行失败：{result.Error}");
        }

        if (string.IsNullOrWhiteSpace(result.DataJson))
        {
            return new JsonObject();
        }

        var node = JsonNode.Parse(result.DataJson);
        return node switch
        {
            JsonObject jsonObject => jsonObject,
            JsonArray or JsonValue => new JsonObject { ["result"] = node },
            _ => new JsonObject(),
        };
    }
}
