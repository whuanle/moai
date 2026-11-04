using MoAI.Auth.Models;

namespace MoAI.Auth.Services;

/// <summary>
/// 图形验证码服务：服务端自绘 SVG（零外部依赖），答案存 Redis，5 分钟有效，连续错 3 次作废.
/// 校验分两种：登录提交时消费（一码一登录），发码前置校验时不消费（答对可继续用于随后的登录提交）.
/// </summary>
public interface ICaptchaService
{
    /// <summary>
    /// 生成图形验证码.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>验证码 id 与图片内容.</returns>
    Task<CaptchaInfo> GenerateAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 校验图形验证码：错误累计 3 次后作废（需刷新重取）；<paramref name="consumeOnSuccess"/> 为 true 时验证通过立即销毁.
    /// </summary>
    /// <param name="captchaId">验证码 id.</param>
    /// <param name="code">用户输入的验证码（不区分大小写）.</param>
    /// <param name="consumeOnSuccess">验证通过后是否销毁.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>是否有效（过期、超次或答案错误均返回 false）.</returns>
    Task<bool> VerifyAsync(string captchaId, string code, bool consumeOnSuccess, CancellationToken cancellationToken);
}
