using System;
using MoAI.Database.Audits;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

/// <summary>
/// 登录风险事件：风控规则命中后生成，供管理员在日志事件页处理（标记已读），与菜单未读角标联动.
/// </summary>
public partial class LoginRiskEventEntity : ICreationAudited
{
    /// <summary>
    /// 主键.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 用户 id.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 用户名.
    /// </summary>
    public string UserName { get; set; } = default!;

    /// <summary>
    /// 规则编码（R1~R6，见 docs/loginsafety/sdd.md）.
    /// </summary>
    public string RuleCode { get; set; } = default!;

    /// <summary>
    /// 风险等级（2=中，3=高）.
    /// </summary>
    public int RiskLevel { get; set; }

    /// <summary>
    /// 事件摘要（面向管理员的一句说明）.
    /// </summary>
    public string Summary { get; set; } = default!;

    /// <summary>
    /// 事件详情（json：ip/属地/UA 等上下文）.
    /// </summary>
    public string Detail { get; set; } = default!;

    /// <summary>
    /// 是否已读（管理员标记后不再计入未读角标）.
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    /// 来源 IP.
    /// </summary>
    public string Ip { get; set; } = default!;

    /// <summary>
    /// 省份.
    /// </summary>
    public string Province { get; set; } = default!;

    /// <summary>
    /// 城市.
    /// </summary>
    public string City { get; set; } = default!;

    /// <inheritdoc/>
    public long CreateUserId { get; set; }

    /// <inheritdoc/>
    public DateTimeOffset CreateTime { get; set; }
}
