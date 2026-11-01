using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Database.Seed;
using MoAI.LoginSafety.Models;
using MoAI.Settings.Services;

namespace MoAI.LoginSafety.Services;

/// <summary>
/// 风险登录通知任务（Hangfire 一次性任务）：读取风险事件详情，按设置模板给用户/管理员发提醒邮件.
/// 从登录主流程异步剥离，SMTP 15s 超时不阻塞登录响应；失败由 Hangfire 自动重试.
/// </summary>
public class LoginRiskNotifyJob
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly DatabaseContext _databaseContext;
    private readonly IEmailSenderService _emailSenderService;
    private readonly ILoginSafetySettingsService _settingsService;
    private readonly ILogger<LoginRiskNotifyJob> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoginRiskNotifyJob"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="emailSenderService">邮件发送服务.</param>
    /// <param name="settingsService">登录安全设置.</param>
    /// <param name="logger">日志.</param>
    public LoginRiskNotifyJob(DatabaseContext databaseContext, IEmailSenderService emailSenderService, ILoginSafetySettingsService settingsService, ILogger<LoginRiskNotifyJob> logger)
    {
        _databaseContext = databaseContext;
        _emailSenderService = emailSenderService;
        _settingsService = settingsService;
        _logger = logger;
    }

    /// <summary>
    /// 通知目标.
    /// </summary>
    public static class Targets
    {
        /// <summary>
        /// 给登录用户本人发提醒.
        /// </summary>
        public const string User = "user";

        /// <summary>
        /// 给管理员发告警.
        /// </summary>
        public const string Admin = "admin";
    }

    /// <summary>
    /// 发送风险事件通知邮件（Hangfire 入口）.
    /// </summary>
    /// <param name="riskEventId">风险事件 id.</param>
    /// <param name="target">通知目标（user / admin）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    [AutomaticRetry(Attempts = 3)]
    public async Task SendAsync(Guid riskEventId, string target, CancellationToken cancellationToken)
    {
        var settings = await _settingsService.GetAsync(cancellationToken);

        var riskEvent = await _databaseContext.LoginRiskEvents.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == riskEventId, cancellationToken);
        if (riskEvent == null)
        {
            _logger.LogWarning("风险事件不存在，跳过通知：{EventId}.", riskEventId);
            return;
        }

        var detail = ParseDetail(riskEvent.Detail);
        var location = JoinLocation(riskEvent.Province, riskEvent.City);
        var now = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss");

        if (target == Targets.User)
        {
            await SendUserAlertAsync(settings, riskEvent, detail, location, now, cancellationToken);
            return;
        }

        await SendAdminAlertAsync(settings, riskEvent, detail, location, now, cancellationToken);
    }

    private async Task SendUserAlertAsync(LoginSafetySettings settings, Database.Entities.LoginRiskEventEntity riskEvent, Dictionary<string, string> detail, string location, string now, CancellationToken cancellationToken)
    {
        var email = detail.TryGetValue("email", out var value) && value.Length > 0 ? value : string.Empty;
        if (email.Length == 0)
        {
            _logger.LogInformation("用户无邮箱，跳过登录提醒：{UserName}.", riskEvent.UserName);
            return;
        }

        var values = new Dictionary<string, string>
        {
            ["userName"] = riskEvent.UserName,
            ["ip"] = riskEvent.Ip,
            ["location"] = location.Length == 0 ? "未知" : location,
            ["time"] = now,
            ["siteName"] = "MoAI"
        };

        var subject = LoginAlertTemplate.Render(Fallback(settings.UserAlertSubject, SettingDefinitions.DefaultUserAlertSubject), values);
        var body = LoginAlertTemplate.Render(Fallback(settings.UserAlertBody, SettingDefinitions.DefaultUserAlertBody), values);

        await _emailSenderService.SendAsync(new MoAI.Settings.Models.EmailMessage
        {
            To = new[] { email },
            Subject = subject,
            Body = body,
            IsHtml = false
        }, cancellationToken);
    }

    private async Task SendAdminAlertAsync(LoginSafetySettings settings, Database.Entities.LoginRiskEventEntity riskEvent, Dictionary<string, string> detail, string location, string now, CancellationToken cancellationToken)
    {
        var recipients = settings.AdminRecipients.Count > 0
            ? settings.AdminRecipients
            : await ResolveAdminEmailsAsync(cancellationToken);

        if (recipients.Count == 0)
        {
            _logger.LogWarning("无可用管理员收件人（admin/root 用户均无邮箱且未配置收件人），跳过告警：{EventId}.", riskEvent.Id);
            return;
        }

        var values = new Dictionary<string, string>
        {
            ["userName"] = riskEvent.UserName,
            ["ip"] = riskEvent.Ip,
            ["location"] = location.Length == 0 ? "未知" : location,
            ["riskLevel"] = ((LoginRiskLevel)riskEvent.RiskLevel).ToString(),
            ["reasons"] = LoginRiskRule.GetTitle(riskEvent.RuleCode),
            ["time"] = now,
            ["siteName"] = "MoAI"
        };

        var subject = LoginAlertTemplate.Render(Fallback(settings.AdminAlertSubject, SettingDefinitions.DefaultAdminAlertSubject), values);
        var body = LoginAlertTemplate.Render(Fallback(settings.AdminAlertBody, SettingDefinitions.DefaultAdminAlertBody), values);

        await _emailSenderService.SendAsync(new MoAI.Settings.Models.EmailMessage
        {
            To = recipients,
            Subject = subject,
            Body = body,
            IsHtml = false
        }, cancellationToken);
    }

    /// <summary>
    /// 解析全部管理员（admin + root）的非空邮箱.
    /// </summary>
    private async Task<List<string>> ResolveAdminEmailsAsync(CancellationToken cancellationToken)
    {
        var adminEmails = await _databaseContext.Users.AsNoTracking()
            .Where(u => u.IsAdmin && u.Email != string.Empty)
            .Select(u => u.Email)
            .ToListAsync(cancellationToken);

        // root 账号通过 setting 表 root 键识别，可能未标记 IsAdmin
        var rootValue = await _databaseContext.Settings.AsNoTracking()
            .Where(s => s.Key == "root")
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken);

        if (rootValue != null && long.TryParse(rootValue, out var rootUserId))
        {
            var rootEmail = await _databaseContext.Users.AsNoTracking()
                .Where(u => u.Id == rootUserId && u.Email != string.Empty)
                .Select(u => u.Email)
                .FirstOrDefaultAsync(cancellationToken);

            if (rootEmail != null && !adminEmails.Contains(rootEmail))
            {
                adminEmails.Add(rootEmail);
            }
        }

        return adminEmails;
    }

    private static Dictionary<string, string> ParseDetail(string detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            return new Dictionary<string, string>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(detail, JsonOptions) ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    private static string JoinLocation(string province, string city)
    {
        return province.Length == 0 ? city : city.Length == 0 || province == city ? province : $"{province} {city}";
    }

    private static string Fallback(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;
}
