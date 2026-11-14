namespace MoAI.App.Queries.Responses;

/// <summary>
/// 访问点接入端点：接入方免组件按 AG-UI 协议直连的 API 地址（绝对地址，随访问点配置匿名下发）.
/// </summary>
public class ExternalAccessPointEndpointsResponse
{
    /// <summary>
    /// AG-UI 对话端点（POST，SSE 流式）.
    /// </summary>
    public string Chat { get; init; } = default!;

    /// <summary>
    /// 会话端点（POST 创建 / GET 列表）.
    /// </summary>
    public string Sessions { get; init; } = default!;

    /// <summary>
    /// 会话消息端点模板，路径含 {sessionId} 占位（GET）.
    /// </summary>
    public string Messages { get; init; } = default!;

    /// <summary>
    /// 访问用户 token 颁发端点（仅内部应用访问点下发，需系统级接入凭据调用）；外部应用为空串.
    /// </summary>
    public string Token { get; init; } = string.Empty;
}
