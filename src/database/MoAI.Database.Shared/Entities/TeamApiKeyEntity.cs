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
/// 团队模型网关API密钥，团队管理员创建并维护，团队成员使用密钥通过 /v1 开放接口调用团队已授权的模型.
/// </summary>
public partial class TeamApiKeyEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 所属团队id.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 密钥名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 密钥前缀，仅用于列表展示，如 moai-Ab12CdEf.
    /// </summary>
    public string KeyPrefix { get; set; } = default!;

    /// <summary>
    /// 密钥的sha256，密钥原文不落库.
    /// </summary>
    public string KeySha256 { get; set; } = default!;

    /// <summary>
    /// 创建密钥的管理员用户id，密钥的可用性与该用户状态绑定.
    /// </summary>
    public long CreatorUserId { get; set; }

    /// <summary>
    /// 是否禁用.
    /// </summary>
    public bool IsDisable { get; set; }

    /// <summary>
    /// 过期时间，null=永不过期.
    /// </summary>
    public DateTimeOffset ExpireTime { get; set; }

    /// <summary>
    /// 最近一次调用时间，null=创建后从未使用.
    /// </summary>
    public DateTimeOffset LastUsedTime { get; set; }

    /// <summary>
    /// 创建人.
    /// </summary>
    public long CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 更新人.
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
