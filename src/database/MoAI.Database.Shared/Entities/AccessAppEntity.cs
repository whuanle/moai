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
/// 应用接入（统一表）：团队级（moai-ac-，TeamId=所属团队）与系统级（TeamId=0，AppType 分型为 subsystem 子系统 / app 应用接入）两类资源域接入.
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
    /// 系统级接入类型：0=不适用（团队级行）1=subsystem 子系统 2=app 应用接入.
    /// </summary>
    public AccessAppType AppType { get; set; }

    /// <summary>
    /// 团队授权范围：0=全员可用 1=指定团队（仅子系统行，委托 SSO 门禁用）.
    /// </summary>
    public SubsystemTeamScope TeamScope { get; set; }

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
    /// 接入密钥存储值（系统级行：v2: 前缀 + AES 密文=可回显，无前缀=存量 PBKDF2 哈希不可回显；
    /// 团队级行：PBKDF2 哈希；null=存量团队接入未设密钥，token 兑换退化为仅 key）.
    /// </summary>
    public string? Secret { get; set; }

    /// <summary>
    /// 密钥展示掩码（前 4 + **** + 后 4，列表回显用；仅系统级可逆格式行填充）.
    /// </summary>
    public string SecretMask { get; set; } = string.Empty;

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
    /// 委托 SSO 登录回调地址列表（JSON 字符串数组，仅子系统行使用），空数组=未开放登录接入；
    /// 子系统引导跳转可经 redirect_uri 指定本次入口（须在列表内，缺省取第一个）.
    /// </summary>
    public string CallbackUrls { get; set; } = "[]";

    /// <summary>
    /// 功能范围位标记（位或组合），限制可访问的资源范围：
    /// 团队级（低位 1..1024）1=model(已下线) 2=wiki_read 4=wiki_write 16=wiki_mcp 32=app_chat 64=kg_read 128=kg_write 256=kg_mcp 512=app_acp 1024=app_a2a；
    /// 系统级不再使用（=0，全量访问 /api/subsys，资源范围分档已下线）.
    /// </summary>
    public int Scopes { get; set; }

    /// <summary>
    /// 最近使用时间（通过模型网关调用时刷新）.
    /// </summary>
    public DateTimeOffset? LastUsedTime { get; set; }
}
