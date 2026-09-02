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
/// 模型渠道.
/// </summary>
public partial class AiChannelEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 渠道标识，对应 models.json 中的 provider id.
    /// </summary>
    public string ProviderKey { get; set; } = default!;

    /// <summary>
    /// 渠道名称.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 协议族（openai/anthropic/google/ollama/custom）.
    /// </summary>
    public int ProtocolFamily { get; set; }

    /// <summary>
    /// 接入端点.
    /// </summary>
    public string BaseUrl { get; set; } = default!;

    /// <summary>
    /// 密钥.
    /// </summary>
    public string ApiKey { get; set; } = default!;

    /// <summary>
    /// 是否启用.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 描述.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 创建人 id.
    /// </summary>
    public long CreateUserId { get; set; }

    /// <summary>
    /// 更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 更新人 id.
    /// </summary>
    public long UpdateUserId { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }
}
