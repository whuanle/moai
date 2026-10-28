using MoAI.Database.Enums;
using MoAI.Infra.Models;

namespace MoAI.Publication.Queries.Responses;

/// <summary>
/// 上架审核记录.
/// </summary>
public class PublicationReviewItem : AuditsInfo
{
    /// <summary>
    /// 上架审核记录 id.
    /// </summary>
    public long PublicationId { get; set; }

    /// <summary>
    /// 资源类型.
    /// </summary>
    public PublicationResourceType ResourceType { get; set; }

    /// <summary>
    /// 资源 id 字符串，应用为 app.id（uuid），提示词为 prompt.id（数字）.
    /// </summary>
    public string ResourceId { get; set; } = string.Empty;

    /// <summary>
    /// 资源名称快照，申请时的资源名称.
    /// </summary>
    public string ResourceName { get; set; } = string.Empty;

    /// <summary>
    /// 资源头像 objectKey（申请时快照之后仍以资源当前头像为准），未设置为空串.
    /// </summary>
    public string ResourceAvatar { get; set; } = string.Empty;

    /// <summary>
    /// 申请上架的版本号（int 编码 aaa*10000 + bb*100 + cc，显示 aaa.bb.cc），仅应用类型有值.
    /// </summary>
    public int? PublishVersion { get; set; }

    /// <summary>
    /// 资源所属团队 id.
    /// </summary>
    public long TeamId { get; set; }

    /// <summary>
    /// 团队名称.
    /// </summary>
    public string TeamName { get; set; } = string.Empty;

    /// <summary>
    /// 申请说明.
    /// </summary>
    public string ApplyReason { get; set; } = string.Empty;

    /// <summary>
    /// 审核状态.
    /// </summary>
    public PublicationState State { get; set; }

    /// <summary>
    /// 审批意见.
    /// </summary>
    public string ReviewComment { get; set; } = string.Empty;

    /// <summary>
    /// 审批时间，未审批为 null.
    /// </summary>
    public DateTimeOffset? ReviewTime { get; set; }
}
