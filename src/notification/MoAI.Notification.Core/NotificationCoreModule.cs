using System;
using Maomi;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Notification.Senders;

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

        // 每种渠道类型一个发送器实现，由 NotificationChannelDispatcher 按类型分发
        context.Services.AddScoped<INotificationChannelSender, Senders.FeishuWebhookSender>();
        context.Services.AddScoped<INotificationChannelSender, Senders.DingTalkWebhookSender>();
        context.Services.AddScoped<INotificationChannelSender, Senders.WecomWebhookSender>();
        context.Services.AddScoped<INotificationChannelSender, Senders.TelegramBotSender>();
        context.Services.AddScoped<INotificationChannelSender, Senders.GenericWebhookSender>();
        context.Services.AddScoped<INotificationChannelSender, Senders.SmtpChannelSender>();
        context.Services.AddScoped<INotificationChannelSender, Senders.InSiteChannelSender>();
    }
}
