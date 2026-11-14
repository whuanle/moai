using System;
using System.Collections.Generic;

namespace MoAI.LoginSafety.Queries.Responses;

/// <summary>
/// 登录风险事件分页响应.
/// </summary>
public class QueryRiskEventListCommandResponse
{
    /// <summary>
    /// 记录集合.
    /// </summary>
    public IReadOnlyList<LoginRiskEventItem> Items { get; set; } = new List<LoginRiskEventItem>();

    /// <summary>
    /// 记录总数.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// 页码（从 1 开始）.
    /// </summary>
    public int PageNo { get; set; }

    /// <summary>
    /// 每页大小.
    /// </summary>
    public int PageSize { get; set; }
}

/// <summary>
/// 登录风险事件记录.
/// </summary>
public class LoginRiskEventItem
{
    /// <summary>
    /// 事件 id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 用户 id.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 用户名.
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// 规则编码（R1~R6）.
    /// </summary>
    public string RuleCode { get; set; } = string.Empty;

    /// <summary>
    /// 风险等级（2=中，3=高）.
    /// </summary>
    public int RiskLevel { get; set; }

    /// <summary>
    /// 事件摘要.
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// 事件详情（json）.
    /// </summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>
    /// 是否已读.
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    /// 来源 IP.
    /// </summary>
    public string Ip { get; set; } = string.Empty;

    /// <summary>
    /// 省份.
    /// </summary>
    public string Province { get; set; } = string.Empty;

    /// <summary>
    /// 城市.
    /// </summary>
    public string City { get; set; } = string.Empty;

    /// <summary>
    /// 事件时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }
}
