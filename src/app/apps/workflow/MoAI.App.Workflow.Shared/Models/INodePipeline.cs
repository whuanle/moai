using System.Text.Json;
using MoAI.Workflow.Enums;

namespace MoAI.Workflow.Models;

/// <summary>
/// 节点管道接口，表示节点的执行记录.
/// 包含节点定义、执行状态、输入输出数据和错误信息.
/// </summary>
public interface INodePipeline
{
    /// <summary>
    /// 节点定义，包含节点的元数据和字段定义.
    /// </summary>
    INodeDefine NodeDefine { get; }

    /// <summary>
    /// 节点执行状态（Pending、Running、Completed、Failed）.
    /// </summary>
    NodeState State { get; }

    /// <summary>
    /// 输入数据的 JSON 元素表示.
    /// 保留原始 JSON 结构用于序列化和存储.
    /// </summary>
    JsonElement InputJsonElement { get; }

    /// <summary>
    /// 输入数据的映射表示，键为字段名称，值为字段值.
    /// 用于程序内部访问和处理.
    /// </summary>
    Dictionary<string, object> InputJsonMap { get; }

    /// <summary>
    /// 输出数据的 JSON 元素表示.
    /// 保留原始 JSON 结构用于序列化和存储.
    /// </summary>
    JsonElement OutputJsonElement { get; }

    /// <summary>
    /// 输出数据的映射表示，键为字段名称，值为字段值.
    /// 用于程序内部访问和处理.
    /// </summary>
    Dictionary<string, object> OutputJsonMap { get; }

    /// <summary>
    /// 错误消息，当节点执行失败时包含错误详情和堆栈跟踪.
    /// </summary>
    string ErrorMessage { get; }
}
