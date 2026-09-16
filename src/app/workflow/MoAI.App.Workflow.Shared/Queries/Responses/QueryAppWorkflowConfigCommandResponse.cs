namespace MoAI.App.Workflow.Queries.Responses;

/// <summary>
/// 流程应用编排配置响应.
/// </summary>
public class QueryAppWorkflowConfigCommandResponse
{
    /// <summary>
    /// 应用 id.
    /// </summary>
    public Guid AppId { get; set; }

    /// <summary>
    /// 编排配置 id，未保存过配置为 null.
    /// </summary>
    public Guid? ConfigId { get; set; }

    /// <summary>
    /// 草稿流程定义 JSON，未保存过为 null.
    /// </summary>
    public string? DraftDefinition { get; set; }

    /// <summary>
    /// 草稿编辑器画布原始 JSON，未保存过为 null.
    /// </summary>
    public string? DraftEditorData { get; set; }

    /// <summary>
    /// 已发布定义快照 JSON，从未发布为 null.
    /// </summary>
    public string? PublishedDefinition { get; set; }

    /// <summary>
    /// 当前已发布版本号，0=从未发布.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// 状态，0=草稿有未发布变更（或从未发布） 1=当前草稿已发布.
    /// </summary>
    public short Status { get; set; }

    /// <summary>
    /// 最近发布时间，从未发布为 null.
    /// </summary>
    public DateTimeOffset? PublishTime { get; set; }
}
