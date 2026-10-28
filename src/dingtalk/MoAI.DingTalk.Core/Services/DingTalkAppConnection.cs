using System;
using System.Threading;
using DingTalkStream.Core;

namespace MoAI.DingTalk.Services;

/// <summary>
/// 单个钉钉应用的 Stream 长连接运行时状态.
/// </summary>
public sealed class DingTalkAppConnection
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DingTalkAppConnection"/> class.
    /// </summary>
    /// <param name="cts">连接取消令牌源.</param>
    /// <param name="appKey">钉钉应用 AppKey.</param>
    /// <param name="appSecret">钉钉应用 AppSecret.</param>
    public DingTalkAppConnection(CancellationTokenSource cts, string appKey, string appSecret)
    {
        Cts = cts;
        AppKey = appKey;
        AppSecret = appSecret;
    }

    /// <summary>
    /// 连接取消令牌源.
    /// </summary>
    public CancellationTokenSource Cts { get; }

    /// <summary>
    /// 建连时的钉钉应用 AppKey（对账用）.
    /// </summary>
    public string AppKey { get; }

    /// <summary>
    /// 建连时的钉钉应用 AppSecret（对账用）.
    /// </summary>
    public string AppSecret { get; }

    /// <summary>
    /// Stream 长连接客户端.
    /// </summary>
    public DingTalkStreamClient? Client { get; set; }

    /// <summary>
    /// 连接后台任务.
    /// </summary>
    public Task? RunTask { get; set; }

    /// <summary>
    /// 是否在线（首次连接成功置 true，生命周期结束置 false）.
    /// </summary>
    public volatile bool IsOnline;
}
