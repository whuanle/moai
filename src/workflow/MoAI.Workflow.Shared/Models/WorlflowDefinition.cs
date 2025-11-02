using MoAI.Workflow.Models.NodeDefinitions;
using System.Text.Json.Serialization;

namespace MoAI.Workflow.Models;

/// <summary>
/// 流程定义.
/// </summary>
public class WorlflowDefinition
{
    /// <summary>
    /// guid.
    /// </summary>
    public Guid Id { get; init; } = Guid.CreateVersion7();

    /// <summary>
    /// 名字.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = default!;

    /// <summary>
    /// 起始节点.
    /// </summary>
    public WorkflowNodefinition StartNode { get; init; } = default!;

    /// <summary>
    /// 结束节点.
    /// </summary>
    public WorkflowNodefinition EndNode { get; init; } = default!;

    /// <summary>
    /// 节点集合.
    /// </summary>
    public IReadOnlyCollection<WorkflowNodefinition> Nodes { get; init; } = new List<WorkflowNodefinition>();

    /// <summary>
    /// 全局变量，只能定义，没有表达式设置.
    /// </summary>
    public IReadOnlyCollection<WorkflowBaseFieldDefinition> GlobalVariables { get; init; } = new List<WorkflowBaseFieldDefinition>();

    /// <summary>
    /// 固定全局系统变量.<br />
    /// sys.user_id<br />
    /// sys.app_id<br />
    /// sys.workflow_id<br />
    /// sys.workflow_run_id<br />
    /// </summary>
    public IReadOnlyCollection<WorkflowFieldDefinition> SystemVariables { get; init; } = new List<WorkflowFieldDefinition>();
}

public enum WorkflowRunStatus
{
    /// <summary>
    /// 运行中.
    /// </summary>
    [JsonPropertyName("running")]
    Running,

    /// <summary>
    /// 已完成.
    /// </summary>
    [JsonPropertyName("completed")]
    Completed,

    /// <summary>
    /// 已取消.
    /// </summary>
    [JsonPropertyName("canceled")]
    Canceled,

    /// <summary>
    /// 异常.
    /// </summary>
    [JsonPropertyName("error")]
    Exception
}

public class WorkflowParamter
{
    /// <summary>
    /// 参数名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 参数值，以 json 字符串存储，使用时再反序列化为对应类型.
    /// </summary>
    public string Value { get; init; } = default!;

    /// <summary>
    /// 参数类型.
    /// </summary>
    public WorkflowFieldType Type { get; init; }

    public IReadOnlyCollection<WorkflowParamter> Children { get; init; } = new List<WorkflowParamter>();
}