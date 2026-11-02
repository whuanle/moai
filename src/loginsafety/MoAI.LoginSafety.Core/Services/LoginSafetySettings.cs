namespace MoAI.LoginSafety.Services;

/// <summary>
/// 登录安全风控设置（来自 setting 表 LS_* 键，未配置回退默认值）.
/// </summary>
public class LoginSafetySettings
{
    /// <summary>
    /// 风控总开关，false 时仅记录登录日志不判定风险.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// 高风险事件推送的通知渠道 ID 列表（来自通知中心 notification_channel，空列表 = 不推送渠道；站内信固定推送不受此影响）.
    /// </summary>
    public IReadOnlyList<long> NotifyChannelIds { get; init; } = Array.Empty<long>();

    /// <summary>
    /// 高风险事件渠道告警的收件人邮箱列表（渠道含 SMTP 邮件渠道时必填，投递给这些邮箱；短信渠道需手机号，暂不支持短信告警）.
    /// </summary>
    public IReadOnlyList<string> NotifyEmails { get; init; } = Array.Empty<string>();

    /// <summary>
    /// IP 失败封禁阈值（滑动窗口内失败次数）.
    /// </summary>
    public int IpFailThreshold { get; init; } = 20;

    /// <summary>
    /// IP 失败计数滑动窗口（分钟）.
    /// </summary>
    public int IpFailWindowMinutes { get; init; } = 10;

    /// <summary>
    /// IP 自动封禁时长（分钟）.
    /// </summary>
    public int IpBanMinutes { get; init; } = 30;

    /// <summary>
    /// 撞库喷洒阈值（窗口内不同用户名失败数）.
    /// </summary>
    public int DistinctUserThreshold { get; init; } = 5;

    /// <summary>
    /// 新 IP 回看天数（0=关闭新 IP 检测）.
    /// </summary>
    public int NewIpDays { get; init; } = 90;

    /// <summary>
    /// 疾速旅行判定窗口（分钟）.
    /// </summary>
    public int TravelWindowMinutes { get; init; } = 120;
}

/// <summary>
/// 登录安全风控设置读取服务.
/// </summary>
public interface ILoginSafetySettingsService
{
    /// <summary>
    /// 读取登录安全风控设置.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回 <see cref="LoginSafetySettings"/>.</returns>
    Task<LoginSafetySettings> GetAsync(CancellationToken cancellationToken);
}
