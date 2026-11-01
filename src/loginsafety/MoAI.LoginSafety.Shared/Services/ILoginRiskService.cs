using MoAI.LoginSafety.Models;

namespace MoAI.LoginSafety.Services;

/// <summary>
/// 登录风控领域服务：登录链路在认证前调用封禁检查，认证结果（成功/失败）提交评估.
/// 评估内部完成：地理解析、规则判定、登录日志与风险事件落库、IP 封禁、邮件通知投递（Hangfire 异步，不阻塞登录）.
/// </summary>
public interface ILoginRiskService
{
    /// <summary>
    /// 检查 IP 是否处于封禁期内（登录请求入口调用），封禁中抛出 403 业务异常.
    /// </summary>
    /// <param name="ip">客户端 IP.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task CheckIpBanAsync(string ip, CancellationToken cancellationToken);

    /// <summary>
    /// 提交一次登录事件进行风控评估：记录登录日志、判定风险、生成风险事件、必要时封禁 IP 并投递通知邮件.
    /// 评估失败不抛出异常（记错误日志），不影响登录主流程.
    /// </summary>
    /// <param name="context">登录事件上下文.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回 <see cref="RiskEvaluationResult"/>，内部异常时返回无风险结果.</returns>
    Task<RiskEvaluationResult> EvaluateAsync(LoginEventContext context, CancellationToken cancellationToken);
}
