using MoAI.Workflow.Enums;
using MoAI.Workflow.Models;

namespace MoAI.Workflow.Queries.Responses;

/// <summary>
/// 查询节点定义响应（批量查询）.
/// </summary>
public class QueryNodeDefineCommandResponse
{
    /// <summary>
    /// 节点定义列表.
    /// </summary>
    public IReadOnlyList<NodeDefineItem> Nodes { get; init; } = Array.Empty<NodeDefineItem>();
}

/// <summary>
/// 节点定义项.
/// </summary>
public class NodeDefineItem
{
    /// <summary>
    /// 节点类型.
    /// </summary>
    public NodeType NodeType { get; init; }

    /// <summary>
    /// 节点类型名称.
    /// </summary>
    public string NodeTypeName { get; init; } = string.Empty;

    /// <summary>
    /// 节点描述信息.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// 输入字段定义列表.
    /// </summary>
    public IReadOnlyList<FieldDefine> InputFields { get; init; } = Array.Empty<FieldDefine>();

    /// <summary>
    /// 输出字段定义列表.
    /// </summary>
    public IReadOnlyList<FieldDefine> OutputFields { get; init; } = Array.Empty<FieldDefine>();

    /// <summary>
    /// 插件 ID（仅 Plugin 节点）.
    /// </summary>
    public int? PluginId { get; init; }

    /// <summary>
    /// 插件名称（仅 Plugin 节点）.
    /// </summary>
    public string? PluginName { get; init; }

    /// <summary>
    /// AI 模型 ID（仅 AiChat 节点）.
    /// </summary>
    public int? ModelId { get; init; }

    /// <summary>
    /// AI 模型名称（仅 AiChat 节点）.
    /// </summary>
    public string? ModelName { get; init; }

    /// <summary>
    /// 知识库 ID（仅 Wiki 节点）.
    /// </summary>
    public int? WikiId { get; init; }

    /// <summary>
    /// 知识库名称（仅 Wiki 节点）.
    /// </summary>
    public string? WikiName { get; init; }

    /// <summary>
    /// 是否支持流式输出.
    /// </summary>
    public bool SupportsStreaming { get; init; }

    /// <summary>
    /// 节点图标（可选）.
    /// </summary>
    public string? Icon { get; init; }

    /// <summary>
    /// 节点颜色（可选，用于 UI 显示）.
    /// </summary>
    public string? Color { get; init; }
}
