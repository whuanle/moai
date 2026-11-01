using System;
using System.Collections.Generic;

namespace MoAI.LoginSafety.Queries.Responses;

/// <summary>
/// 本人登录记录分页响应.
/// </summary>
public class QueryMyLoginLogCommandResponse
{
    /// <summary>
    /// 记录集合.
    /// </summary>
    public IReadOnlyList<MyLoginLogItem> Items { get; set; } = new List<MyLoginLogItem>();

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
/// 本人登录记录.
/// </summary>
public class MyLoginLogItem
{
    /// <summary>
    /// 记录 id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 登录方式（0=账号密码，1=OAuth）.
    /// </summary>
    public int LoginType { get; set; }

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
    /// 登录时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }
}
