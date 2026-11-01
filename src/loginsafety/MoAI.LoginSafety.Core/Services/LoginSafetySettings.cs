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
    /// 中/高风险登录给用户本人发提醒邮件.
    /// </summary>
    public bool NotifyUserEnabled { get; init; }

    /// <summary>
    /// 高风险事件给管理员发告警邮件.
    /// </summary>
    public bool NotifyAdminEnabled { get; init; } = true;

    /// <summary>
    /// 管理员告警额外收件人（空列表 = 全部 admin/root 用户邮箱）.
    /// </summary>
    public IReadOnlyList<string> AdminRecipients { get; init; } = Array.Empty<string>();

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

    /// <summary>
    /// 用户提醒邮件主题模板.
    /// </summary>
    public string UserAlertSubject { get; init; } = string.Empty;

    /// <summary>
    /// 用户提醒邮件正文模板.
    /// </summary>
    public string UserAlertBody { get; init; } = string.Empty;

    /// <summary>
    /// 管理员告警邮件主题模板.
    /// </summary>
    public string AdminAlertSubject { get; init; } = string.Empty;

    /// <summary>
    /// 管理员告警邮件正文模板.
    /// </summary>
    public string AdminAlertBody { get; init; } = string.Empty;
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
