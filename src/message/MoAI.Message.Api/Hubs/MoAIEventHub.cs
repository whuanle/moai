using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.SignalR;

namespace MoAI.Message.Hubs;

/// <summary>
/// 平台事件推送 Hub（/hub/moai）：只推事件不推数据，客户端收到 onEvent 后调用对应 REST 接口取详情；
/// 服务端不提供客户端可调用的方法（单向推送），同用户多端连接由 UserIdProvider 归组.
/// </summary>
/// <remarks>
/// 必须禁用 HTTP 日志：UseHttpLogging 的请求体缓冲会让 SignalR 握手读到空输入（HandshakeFailed），
/// WebSocket/长轮询传输请求一律不记请求体.
/// </remarks>
[Authorize]
[HttpLogging(HttpLoggingFields.None)]
public class MoAIEventHub : Hub
{
}
