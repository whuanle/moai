using System.Threading;
using System.Threading.Tasks;
using MoAI.Infra.WeixinWork.Models;
using Refit;

namespace MoAI.Infra.WeixinWork;

/// <summary>
/// 企业微信群机器人客户端（固定域名 <c>qyapi.weixin.qq.com</c>，服务地址可被 <c>MoAI:WeixinWork:RobotEndpoint</c> 配置覆盖用于本地桩联调）.
/// </summary>
public interface IWeixinWorkRobotClient
{
    /// <summary>
    /// 推送 text 消息.
    /// </summary>
    /// <param name="key">机器人 key.</param>
    /// <param name="request">text 消息体.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>响应（errcode/errmsg）.</returns>
    [Post("/cgi-bin/webhook/send")]
    Task<WeixinWorkRobotResponse> SendTextAsync([Query, AliasAs("key")] string key, [Body] WeixinWorkRobotTextRequest request, CancellationToken cancellationToken = default);
}
