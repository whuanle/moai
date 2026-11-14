using System;
using MoAI.Database.Audits;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

/// <summary>
/// 登录日志：记录每一次登录尝试（成功/失败）与来源环境、风险评估结果，由登录风控写入，追加不改.
/// </summary>
public partial class LoginLogEntity : ICreationAudited
{
    /// <summary>
    /// 主键.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 用户 id，用户不存在时为 0.
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 登录名（用户名或邮箱尝试值）.
    /// </summary>
    public string UserName { get; set; } = default!;

    /// <summary>
    /// 登录方式（0=账号密码，1=第三方 OAuth）.
    /// </summary>
    public int LoginType { get; set; }

    /// <summary>
    /// 是否登录成功.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 失败原因，成功时为空.
    /// </summary>
    public string FailReason { get; set; } = default!;

    /// <summary>
    /// 来源 IP.
    /// </summary>
    public string Ip { get; set; } = default!;

    /// <summary>
    /// 国家（IP 归属地，无地理库时为空）.
    /// </summary>
    public string Country { get; set; } = default!;

    /// <summary>
    /// 省份.
    /// </summary>
    public string Province { get; set; } = default!;

    /// <summary>
    /// 城市.
    /// </summary>
    public string City { get; set; } = default!;

    /// <summary>
    /// 运营商.
    /// </summary>
    public string Isp { get; set; } = default!;

    /// <summary>
    /// User-Agent.
    /// </summary>
    public string UserAgent { get; set; } = default!;

    /// <summary>
    /// 风险等级（0=无，1=低，2=中，3=高）.
    /// </summary>
    public int RiskLevel { get; set; }

    /// <summary>
    /// 命中的风险规则说明（json 数组），无风险时为空.
    /// </summary>
    public string RiskReasons { get; set; } = default!;

    /// <inheritdoc/>
    public long CreateUserId { get; set; }

    /// <inheritdoc/>
    public DateTimeOffset CreateTime { get; set; }
}
