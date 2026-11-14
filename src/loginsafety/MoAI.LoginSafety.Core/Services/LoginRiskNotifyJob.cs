using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.LoginSafety.Models;
using MoAI.Message.Models;
using MoAI.Message.Services;
using MoAI.Notification.Models;
using MoAI.Notification.Services;

namespace MoAI.LoginSafety.Services;

/// <summary>
/// 风险登录通知任务（Hangfire 一次性任务）：读取风险事件详情，
/// 中/高风险登录成功给受影响用户发站内信（固定逻辑），高风险事件推送到设置的通知渠道（未配置渠道时跳过）.
/// 从登录主流程异步剥离，不阻塞登录响应；失败由 Hangfire 自动重试.
/// 通知标题/正文由内置双语模板（<see cref="LoginAlertTemplates"/>）生成，默认中文；
/// 渠道选 SMTP 时邮件主题=标题（渠道前缀）、正文=正文，模板内置不可定制.
/// </summary>
public class LoginRiskNotifyJob
{
    /// <summary>通知来源标识（站内信 Source 与通知渠道 Source 共用）.</summary>
    public const string Source = "loginsafety";

    private readonly DatabaseContext _databaseContext;
    private readonly IMessageService _messageService;
    private readonly INotificationService _notificationService;
    private readonly ILoginSafetySettingsService _settingsService;
    private readonly ILogger<LoginRiskNotifyJob> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoginRiskNotifyJob"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="messageService">站内信领域服务.</param>
    /// <param name="notificationService">通知中心领域服务.</param>
    /// <param name="settingsService">登录安全设置.</param>
    /// <param name="logger">日志.</param>
    public LoginRiskNotifyJob(DatabaseContext databaseContext, IMessageService messageService, INotificationService notificationService, ILoginSafetySettingsService settingsService, ILogger<LoginRiskNotifyJob> logger)
    {
        _databaseContext = databaseContext;
        _messageService = messageService;
        _notificationService = notificationService;
        _settingsService = settingsService;
        _logger = logger;
    }

    /// <summary>
    /// 通知目标.
    /// </summary>
    public static class Targets
    {
        /// <summary>
        /// 给受影响用户本人发站内信提醒.
        /// </summary>
        public const string User = "user";

        /// <summary>
        /// 推送高风险告警到已配置的通知渠道.
        /// </summary>
        public const string Channels = "channels";
    }

    /// <summary>
    /// 发送风险事件通知（Hangfire 入口）.
    /// </summary>
    /// <param name="riskEventId">风险事件 id.</param>
    /// <param name="target">通知目标（user / channels）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    [AutomaticRetry(Attempts = 3)]
    public async Task SendAsync(Guid riskEventId, string target, CancellationToken cancellationToken)
    {
        var riskEvent = await _databaseContext.LoginRiskEvents.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == riskEventId, cancellationToken);
        if (riskEvent == null)
        {
            _logger.LogWarning("风险事件不存在，跳过通知：{EventId}.", riskEventId);
            return;
        }

        if (target == Targets.User)
        {
            await SendUserInSiteAsync(riskEvent, cancellationToken);
            return;
        }

        var settings = await _settingsService.GetAsync(cancellationToken);
        await SendChannelAlertAsync(settings, riskEvent, cancellationToken);
    }

    /// <summary>
    /// 给受影响用户本人发站内信提醒（固定逻辑，无开关）.
    /// </summary>
    private async Task SendUserInSiteAsync(Database.Entities.LoginRiskEventEntity riskEvent, CancellationToken cancellationToken)
    {
        if (riskEvent.UserId <= 0)
        {
            _logger.LogInformation("风险事件无归属用户，跳过站内信：{EventId}.", riskEvent.Id);
            return;
        }

        var language = LoginAlertTemplates.ResolveLanguage(null);
        var location = JoinLocation(riskEvent.Province, riskEvent.City);
        var values = new Dictionary<string, string>
        {
            ["userName"] = riskEvent.UserName,
            ["ip"] = riskEvent.Ip,
            ["location"] = location.Length == 0 ? "未知" : location,
            ["time"] = riskEvent.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"),
            ["siteName"] = "MoAI"
        };

        var (title, content) = LoginAlertTemplates.UserAlert(language, values);
        await _messageService.SendToUsersAsync(new MessageSendItem
        {
            Title = title,
            Content = content,
            MessageType = MessageType.System,
            Source = Source
        }, new[] { riskEvent.UserId }, cancellationToken);
    }

    /// <summary>
    /// 推送高风险告警到已配置的通知渠道（未配置时跳过；停用/已删渠道由通知中心自动过滤）.
    /// </summary>
    private async Task SendChannelAlertAsync(LoginSafetySettings settings, Database.Entities.LoginRiskEventEntity riskEvent, CancellationToken cancellationToken)
    {
        if (settings.NotifyChannelIds.Count == 0)
        {
            _logger.LogInformation("未配置通知渠道，跳过高风险渠道告警：{EventId}.", riskEvent.Id);
            return;
        }

        var language = LoginAlertTemplates.ResolveLanguage(null);
        var location = JoinLocation(riskEvent.Province, riskEvent.City);
        var values = new Dictionary<string, string>
        {
            ["userName"] = riskEvent.UserName,
            ["ip"] = riskEvent.Ip,
            ["location"] = location.Length == 0 ? "未知" : location,
            ["riskLevel"] = LoginAlertTemplates.RiskLevelText((LoginRiskLevel)riskEvent.RiskLevel, language),
            ["reasons"] = LoginRiskRule.GetTitle(riskEvent.RuleCode),
            ["time"] = riskEvent.CreateTime.ToString("yyyy-MM-dd HH:mm:ss"),
            ["siteName"] = "MoAI"
        };

        var (title, content) = LoginAlertTemplates.HighRiskAlert(language, values);
        await _notificationService.NotifyAsync(new NotificationMessage
        {
            Title = title,
            Content = content,
            Level = NotificationLevel.Error,
            Source = Source,
            EmailRecipients = settings.NotifyEmails
        }, new NotificationSendOptions { ChannelIds = settings.NotifyChannelIds }, cancellationToken);
    }

    private static string JoinLocation(string province, string city)
    {
        return province.Length == 0 ? city : city.Length == 0 || province == city ? province : $"{province} {city}";
    }
}
