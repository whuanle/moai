using System.Threading;
using System.Threading.Tasks;
using MoAI.Infra.DingTalk.Models;
using Refit;

namespace MoAI.Infra.DingTalk;

/// <summary>
/// 钉钉群自定义机器人客户端（固定域名 <c>oapi.dingtalk.com</c>，服务地址可被 <c>MoAI:DingTalk:RobotEndpoint</c> 配置覆盖用于本地桩联调）.
/// </summary>
/// <remarks>
/// 加签机器人把 <c>timestamp</c> 与 <c>sign</c> 放在查询串（由调用方计算 HMAC-SHA256 后传入），access_token 同为查询参数.
/// </remarks>
public interface IDingTalkRobotClient
{
    /// <summary>
    /// 推送 text 消息.
    /// </summary>
    /// <param name="accessToken">机器人 access_token.</param>
    /// <param name="timestamp">加签时间戳（毫秒，未加签传 null）.</param>
    /// <param name="sign">加签结果（URL 编码后，未加签传 null）.</param>
    /// <param name="request">text 消息体.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>响应（errcode/errmsg）.</returns>
    [Post("/robot/send")]
    Task<DingTalkRobotResponse> SendTextAsync([Query, AliasAs("access_token")] string accessToken, [Query] string? timestamp, [Query] string? sign, [Body] DingTalkRobotTextRequest request, CancellationToken cancellationToken = default);
}
