using System;
using System.Collections.Generic;

namespace MoAI.LoginSafety.Queries.Responses;

/// <summary>
/// IP 封禁记录分页响应.
/// </summary>
public class QueryIpBanListCommandResponse
{
    /// <summary>
    /// 记录集合.
    /// </summary>
    public IReadOnlyList<LoginIpBanItem> Items { get; set; } = new List<LoginIpBanItem>();

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
/// IP 封禁记录.
/// </summary>
public class LoginIpBanItem
{
    /// <summary>
    /// 记录 id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 封禁的 IP.
    /// </summary>
    public string Ip { get; set; } = string.Empty;

    /// <summary>
    /// 封禁原因.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// 来源（auto:R1 / auto:R2 / manual）.
    /// </summary>
    public string SourceRule { get; set; } = string.Empty;

    /// <summary>
    /// 封禁到期时间.
    /// </summary>
    public DateTimeOffset ExpireTime { get; set; }

    /// <summary>
    /// 是否已解封.
    /// </summary>
    public bool IsLifted { get; set; }

    /// <summary>
    /// 封禁时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }
}
