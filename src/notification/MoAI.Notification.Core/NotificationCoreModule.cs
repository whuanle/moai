using System;
using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Notification.Senders;
using MoAI.Settings.Services;

namespace MoAI.Notification;

/// <summary>
/// NotificationCoreModule.
/// </summary>
[InjectModule<NotificationSharedModule>]
[InjectModule<NotificationApiModule>]
public class NotificationCoreModule : IModule
{
    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        // 外部渠道 HTTP 客户端：统一 15s 超时，避免慢渠道拖垮后台任务
        context.Services.AddHttpClient(SenderHttp.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        // SMTP 邮件传输：渠道广播与系统「邮箱验证」绑定发送共用
        context.Services.AddScoped<SmtpMailTransport>();

        // 平台系统邮件发送能力由通知中心承接：按「邮箱验证」绑定的 SMTP 渠道发送
        context.Services.AddScoped<IEmailSenderService, SystemEmailSenderService>();

        // 每种渠道类型一个发送器实现，由 NotificationChannelDispatcher 按类型分发
        context.Services.AddScoped<INotificationChannelSender, Senders.FeishuWebhookSender>();
        context.Services.AddScoped<INotificationChannelSender, Senders.DingTalkWebhookSender>();
        context.Services.AddScoped<INotificationChannelSender, Senders.WecomWebhookSender>();
        context.Services.AddScoped<INotificationChannelSender, Senders.FeishuAppBotSender>();
        context.Services.AddScoped<INotificationChannelSender, Senders.DingTalkAppBotSender>();
        context.Services.AddScoped<INotificationChannelSender, Senders.WecomAppBotSender>();
        context.Services.AddScoped<INotificationChannelSender, Senders.TelegramBotSender>();
        context.Services.AddScoped<INotificationChannelSender, Senders.GenericWebhookSender>();
        context.Services.AddScoped<INotificationChannelSender, Senders.SmtpChannelSender>();
        context.Services.AddScoped<INotificationChannelSender, Senders.InSiteChannelSender>();
        context.Services.AddScoped<INotificationChannelSender, Senders.SlackWebhookSender>();
        context.Services.AddScoped<INotificationChannelSender, Senders.DiscordWebhookSender>();
        context.Services.AddScoped<INotificationChannelSender, Senders.MicrosoftTeamsWebhookSender>();
        context.Services.AddScoped<INotificationChannelSender, Senders.GotifySender>();
        context.Services.AddScoped<INotificationChannelSender, Senders.NtfySender>();
        context.Services.AddScoped<INotificationChannelSender, Senders.BarkSender>();
        context.Services.AddScoped<INotificationChannelSender, Senders.ServerChanSender>();
        context.Services.AddScoped<INotificationChannelSender, Senders.PushPlusSender>();
        context.Services.AddScoped<INotificationChannelSender, Senders.AliyunSmsSender>();
        context.Services.AddScoped<INotificationChannelSender, Senders.TencentSmsSender>();
    }
}
