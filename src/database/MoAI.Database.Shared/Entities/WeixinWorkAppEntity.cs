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
/// 企业微信应用，一个企业微信管理端自建应用（CorpId + AgentId + Secret）对应一条连接，
/// 消息经回调 URL 统一接收后按绑定转发.
/// </summary>
public partial class WeixinWorkAppEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// 连接名称，团队内唯一.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// 团队id.
    /// </summary>
    public int TeamId { get; set; }

    /// <summary>
    /// 企业微信企业 ID（管理后台 我的企业 → 企业信息），与 AgentId 联合全局唯一.
    /// </summary>
    public string CorpId { get; set; } = default!;

    /// <summary>
    /// 企业微信自建应用 Secret（应用管理 → 自建应用 → Secret）.
    /// </summary>
    public string CorpSecret { get; set; } = default!;

    /// <summary>
    /// 企业微信自建应用 AgentId.
    /// </summary>
    public int AgentId { get; set; }

    /// <summary>
    /// 接收消息回调配置 Token（管理后台 接收消息 → 设置 API 接收），用于回调签名校验.
    /// </summary>
    public string Token { get; set; } = default!;

    /// <summary>
    /// 接收消息回调配置 EncodingAESKey（43 字符），用于回调消息加解密.
    /// </summary>
    public string EncodingAesKey { get; set; } = default!;

    /// <summary>
    /// API 地址覆盖（默认 https://qyapi.weixin.qq.com），供本地桩联调或代理使用，留空走官方.
    /// </summary>
    public string ApiEndpoint { get; set; } = default!;

    /// <summary>
    /// 禁用，禁用后回调推送不再处理.
    /// </summary>
    public bool IsDisable { get; set; }

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
