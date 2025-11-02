using System.Text.Json.Serialization;

namespace MoAI.Workflow.Engines;

/// <summary>
/// 节点运行状态.
/// </summary>
public enum WorkflowNodeRunStatus
{
    /// <summary>
    /// 运行中.
    /// </summary>
    [JsonPropertyName("running")]
    Running,

    /// <summary>
    /// 执行输出中，节点运行完成，但是正在生成输出.
    /// </summary>
    [JsonPropertyName("outputing")]
    Outputing,

    /// <summary>
    /// 重试中.
    /// </summary>
    [JsonPropertyName("retrying")]
    Retrying,

    /// <summary>
    /// 成功.
    /// </summary>
    [JsonPropertyName("success")]
    Success,

    /// <summary>
    /// 失败.
    /// </summary>
    [JsonPropertyName("failed")]
    Failed,

    /// <summary>
    /// 取消.
    /// </summary>
    [JsonPropertyName("canceled")]
    Canceled,

    /// <summary>
    /// 异常.
    /// </summary>
    [JsonPropertyName("exception")]
    Exception,
}