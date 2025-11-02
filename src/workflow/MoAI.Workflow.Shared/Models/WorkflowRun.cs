namespace MoAI.Workflow.Models;

/// <summary>
/// 启动流程的运行参数.
/// </summary>
public class WorkflowRun
{
    /// <summary>
    /// 实例.
    /// </summary>
    public Guid InstanceId { get; init; } = Guid.CreateVersion7();

    /// <summary>
    /// 流程定义的 id.
    /// </summary>
    public Guid DefinitionId { get; init; } = Guid.CreateVersion7();

    /// <summary>
    /// 应用 id.
    /// </summary>
    public Guid AppId { get; init; } = Guid.CreateVersion7();

    /// <summary>
    /// 发起请求的用户 id.
    /// </summary>
    public string UserId { get; init; } = default!;

    /// <summary>
    /// 启动此流程使用的原始输入，json 结构.
    /// </summary>
    public string InputJson { get; init; } = default!;
}
