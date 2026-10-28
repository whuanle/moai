using System;
using System.Threading;
using FeishuWss.Client;

namespace MoAI.Feishu.Services;

/// <summary>
/// 单个飞书应用的长连接运行时状态.
/// </summary>
public sealed class FeishuAppConnection
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FeishuAppConnection"/> class.
    /// </summary>
    /// <param name="cts">连接取消令牌源.</param>
    /// <param name="appId">飞书开放平台 AppID.</param>
    /// <param name="appSecret">飞书开放平台 AppSecret.</param>
    /// <param name="domain">接入域名.</param>
    public FeishuAppConnection(CancellationTokenSource cts, string appId, string appSecret, string domain)
    {
        Cts = cts;
        AppId = appId;
        AppSecret = appSecret;
        Domain = domain;
    }

    /// <summary>
    /// 连接取消令牌源.
    /// </summary>
    public CancellationTokenSource Cts { get; }

    /// <summary>
    /// 建连时的飞书开放平台 AppID（对账用）.
    /// </summary>
    public string AppId { get; }

    /// <summary>
    /// 建连时的飞书开放平台 AppSecret（对账用）.
    /// </summary>
    public string AppSecret { get; }

    /// <summary>
    /// 建连时的接入域名（对账用）.
    /// </summary>
    public string Domain { get; }

    /// <summary>
    /// 长连接客户端.
    /// </summary>
    public WssClient? Client { get; set; }

    /// <summary>
    /// 连接后台任务.
    /// </summary>
    public Task? RunTask { get; set; }

    /// <summary>
    /// 是否在线（首次连接成功置 true，断开置 false）.
    /// </summary>
    public volatile bool IsOnline;
}
