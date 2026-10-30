using System;
using System.Collections.Generic;
using MoAI.Database.Audits;
using MoAI.Database.Enums;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

/// <summary>
/// 应用接入（统一表）：团队级（moai-ac-，TeamId=所属团队）与系统级（moai-sa-，TeamId=0）两类资源域接入.
/// </summary>
public partial class AccessAppEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 应用名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 团队id（系统级接入固定 0）.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 接入层级：team=团队级 / system=系统级，决定可访问的资源面.
    /// </summary>
    public AccessAppLevel Level { get; set; }

    /// <summary>
    /// key 展示前缀（列表回显用）.
    /// </summary>
    public string KeyPrefix { get; set; } = string.Empty;

    /// <summary>
    /// 接入密钥 PBKDF2 哈希（创建/重置时返回原文一次；null=存量接入未设密钥，token 兑换退化为仅 key）.
    /// </summary>
    public string? Secret { get; set; }

    /// <summary>
    /// 接入密钥盐（Base64）.
    /// </summary>
    public string? SecretSalt { get; set; }

    /// <summary>
    /// 状态：停用后协作面调用立即拒绝.
    /// </summary>
    public AccessAppStatus Status { get; set; }

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

    /// <summary>
    /// key.
    /// </summary>
    public string Key { get; set; } = default!;

    /// <summary>
    /// 功能范围位标记（位或组合），限制可访问的资源范围：
    /// 团队级（低位 1..1024）1=model(已下线) 2=wiki_read 4=wiki_write 16=wiki_mcp 32=app_chat 64=kg_read 128=kg_write 256=kg_mcp 512=app_acp 1024=app_a2a；
    /// 系统级（高位 1&lt;&lt;16 起）65536=org_read 131072=org_write 262144=user_read 524288=user_write 1048576=team_read 2097152=team_write.
    /// </summary>
    public int Scopes { get; set; }

    /// <summary>
    /// 最近使用时间（通过模型网关调用时刷新）.
    /// </summary>
    public DateTimeOffset? LastUsedTime { get; set; }
}
