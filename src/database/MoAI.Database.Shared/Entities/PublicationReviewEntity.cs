using System;
using System.Collections.Generic;
using MoAI.Database.Audits;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

/// <summary>
/// 上架审核，资源公开到平台前需系统管理员审批.
/// </summary>
public partial class PublicationReviewEntity : IFullAudited
{
    /// <summary>
    /// 自增主键.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 资源类型，见 PublicationResourceType（应用=0，提示词=1）.
    /// </summary>
    public int ResourceType { get; set; }

    /// <summary>
    /// 资源 id 字符串，应用为 app.id（uuid），提示词为 prompt.id（数字）.
    /// </summary>
    public string ResourceId { get; set; } = default!;

    /// <summary>
    /// 资源名称快照，申请时的资源名称.
    /// </summary>
    public string ResourceName { get; set; } = default!;

    /// <summary>
    /// 资源所属团队id.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 申请说明.
    /// </summary>
    public string ApplyReason { get; set; } = default!;

    /// <summary>
    /// 审核状态，见 PublicationState（待审核=0，已通过=1，已驳回=2）.
    /// </summary>
    public int State { get; set; }

    /// <summary>
    /// 审批意见.
    /// </summary>
    public string ReviewComment { get; set; } = default!;

    /// <summary>
    /// 审批时间，未审批为 null.
    /// </summary>
    public DateTimeOffset? ReviewTime { get; set; }

    /// <summary>
    /// 创建人（申请人）.
    /// </summary>
    public long CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 更新人（审批人）.
    /// </summary>
    public long UpdateUserId { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }
}
