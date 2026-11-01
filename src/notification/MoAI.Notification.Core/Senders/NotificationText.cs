using MoAI.Database.Enums;
using MoAI.Notification.Models;

namespace MoAI.Notification.Senders;

/// <summary>
/// 通知内容格式化：把统一通知消息渲染为各渠道使用的纯文本/Markdown 形态.
/// </summary>
public static class NotificationText
{
    /// <summary>
    /// 级别图标.
    /// </summary>
    /// <param name="level">通知级别.</param>
    /// <returns>返回图标字符串.</returns>
    public static string Icon(NotificationLevel level) => level switch
    {
        NotificationLevel.Warning => "⚠️",
        NotificationLevel.Error => "❌",
        _ => "ℹ️",
    };

    /// <summary>
    /// 级别 json 名（webhook 模板占位符用）.
    /// </summary>
    /// <param name="level">通知级别.</param>
    /// <returns>返回 info / warning / error.</returns>
    public static string LevelName(NotificationLevel level) => level switch
    {
        NotificationLevel.Warning => "warning",
        NotificationLevel.Error => "error",
        _ => "info",
    };

    /// <summary>
    /// 当前时间文本（通知脚注用）.
    /// </summary>
    /// <returns>返回 yyyy-MM-dd HH:mm:ss.</returns>
    public static string Time() => DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>
    /// 纯文本形态（飞书/钉钉/Telegram/邮件正文/通用模板缺省）.
    /// </summary>
    /// <param name="message">通知消息.</param>
    /// <returns>返回纯文本.</returns>
    public static string Plain(NotificationMessage message)
    {
        var text = $"【MoAI】{Icon(message.Level)} {message.Title}\n{message.Content}";
        return message.Source.Length == 0 ? text : $"{text}\n\n来源：{message.Source}";
    }

    /// <summary>
    /// Markdown 形态（企业微信/通用 webhook）.
    /// </summary>
    /// <param name="message">通知消息.</param>
    /// <returns>返回 Markdown 文本.</returns>
    public static string Markdown(NotificationMessage message)
    {
        var text = $"**【MoAI】{Icon(message.Level)} {message.Title}**\n{message.Content}";
        var footer = $"\n> {NotificationText.Time()}";
        return message.Source.Length == 0 ? $"{text}{footer}" : $"{text}\n> 来源：{message.Source}{footer}";
    }

    /// <summary>
    /// 截断文本（渠道字数上限与日志存档用）.
    /// </summary>
    /// <param name="value">原文.</param>
    /// <param name="maxLength">最大长度.</param>
    /// <returns>返回截断后的文本.</returns>
    public static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
