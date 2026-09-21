namespace MoAI.Feishu.Models;

/// <summary>
/// 飞书知识空间节点（wiki 节点）信息.
/// </summary>
public class FeishuWikiNode
{
    /// <summary>
    /// 节点 token，知识库节点的唯一标识.
    /// </summary>
    public string NodeToken { get; init; } = default!;

    /// <summary>
    /// 节点关联的实际文档 token（docx/sheet/bitable 的 token）.
    /// </summary>
    public string ObjToken { get; init; } = default!;

    /// <summary>
    /// 文档类型：docx / sheet / bitable / doc 等.
    /// </summary>
    public string ObjType { get; init; } = default!;

    /// <summary>
    /// 节点标题（文档名）.
    /// </summary>
    public string Title { get; init; } = default!;

    /// <summary>
    /// 所属知识空间 id.
    /// </summary>
    public string SpaceId { get; init; } = default!;

    /// <summary>
    /// 是否含有子节点.
    /// </summary>
    public bool HasChild { get; init; }

    /// <summary>
    /// 父节点 token，根节点为空.
    /// </summary>
    public string? ParentNodeToken { get; init; }

    /// <summary>
    /// 文档最近编辑时间（毫秒时间戳字符串），可作为版本号辅助判断变化.
    /// </summary>
    public string? ObjEditTime { get; init; }
}
