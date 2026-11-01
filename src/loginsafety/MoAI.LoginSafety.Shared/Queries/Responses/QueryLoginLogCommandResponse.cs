using System;
using System.Collections.Generic;

namespace MoAI.LoginSafety.Queries.Responses;

/// <summary>
/// 登录日志分页响应.
/// </summary>
public class QueryLoginLogCommandResponse
{
    /// <summary>
    /// 记录集合.
    /// </summary>
    public IReadOnlyList<LoginLogItem> Items { get; set; } = new List<LoginLogItem>();

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
/// 登录日志记录.
/// </summary>
public class LoginLogItem
{
    /// <summary>
    /// 记录 id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 用户 id，用户不存在时为 0.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 用户名（登录尝试值）.
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// 登录方式（0=账号密码，1=OAuth）.
    /// </summary>
    public int LoginType { get; set; }

    /// <summary>
    /// 是否登录成功.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 失败原因，成功时为空.
    /// </summary>
    public string FailReason { get; set; } = string.Empty;

    /// <summary>
    /// 来源 IP.
    /// </summary>
    public string Ip { get; set; } = string.Empty;

    /// <summary>
    /// 国家.
    /// </summary>
    public string Country { get; set; } = string.Empty;

    /// <summary>
    /// 省份.
    /// </summary>
    public string Province { get; set; } = string.Empty;

    /// <summary>
    /// 城市.
    /// </summary>
    public string City { get; set; } = string.Empty;

    /// <summary>
    /// 运营商.
    /// </summary>
    public string Isp { get; set; } = string.Empty;

    /// <summary>
    /// User-Agent.
    /// </summary>
    public string UserAgent { get; set; } = string.Empty;

    /// <summary>
    /// 风险等级（0~3）.
    /// </summary>
    public int RiskLevel { get; set; }

    /// <summary>
    /// 命中的风险规则说明（json 数组）.
    /// </summary>
    public string RiskReasons { get; set; } = string.Empty;

    /// <summary>
    /// 登录时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }
}
