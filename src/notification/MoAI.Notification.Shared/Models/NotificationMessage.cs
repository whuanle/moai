using MoAI.Database.Enums;

namespace MoAI.Notification.Models;

/// <summary>
/// 统一通知消息：业务方只描述「发什么」，不感知任何外部渠道接口格式.
/// </summary>
public class NotificationMessage
{
    /// <summary>
    /// 标题.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 正文（纯文本，可包含换行）.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// 通知级别.
    /// </summary>
    public NotificationLevel Level { get; init; } = NotificationLevel.Info;

    /// <summary>
    /// 来源模块标识，如 loginsafety / timer，空为平台.
    /// </summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// 定向站内用户 id 集合（可选）：仅应用机器人渠道（feishuAppBot/dingtalkAppBot/wecomAppBot）生效，
    /// 只投递这些用户中已绑定平台账号者；空表示投递全部绑定用户。渠道测试场景定向当前管理员.
    /// </summary>
    public IReadOnlyList<long>? TargetUserIds { get; init; }

    /// <summary>
    /// 收件人邮箱列表（可选）：仅 SMTP 渠道生效，发送时动态指定（广播/试发由管理员填写，
    /// 系统「邮箱验证」走 IEmailSenderService 不经此字段）；缺失时该渠道投递失败并留痕.
    /// </summary>
    public IReadOnlyList<string>? EmailRecipients { get; init; }

    /// <summary>
    /// 收件人手机号列表（可选）：仅短信渠道（aliyunSms/tencentSms）生效，发送时动态指定；
    /// 缺失时该渠道投递失败并留痕.
    /// </summary>
    public IReadOnlyList<string>? PhoneNumbers { get; init; }

    /// <summary>
    /// 使用的通知模板 id（可选）：由领域服务在投递前解析渲染为最终文本.
    /// </summary>
    public long? TemplateId { get; init; }

    /// <summary>
    /// 模板变量值表（可选）：与模板的变量定义按 name 对应，缺失变量在解析时报错.
    /// </summary>
    public IReadOnlyDictionary<string, string>? TemplateVariables { get; init; }

    /// <summary>
    /// 内容类型：默认纯文本；模板为 HTML 时由领域服务置为 Html，SMTP 渠道按 HTML 邮件发送，其它渠道降级纯文本.
    /// </summary>
    public NotificationContentType ContentType { get; init; } = NotificationContentType.Text;

    /// <summary>
    /// 阿里云短信模板 Code（领域服务解析模板时填充；短信渠道发送用，多渠道投递互不干扰）.
    /// </summary>
    public string? SmsAliyunTemplateCode { get; set; }

    /// <summary>
    /// 腾讯云短信模板 Id（领域服务解析模板时填充；短信渠道发送用，多渠道投递互不干扰）.
    /// </summary>
    public string? SmsTencentTemplateId { get; set; }

    /// <summary>
    /// 短信模板参数名顺序（领域服务解析模板时按正文占位符出现顺序提取；腾讯云 TemplateParamSet 为位置数组，顺序必须与平台模板一致）.
    /// </summary>
    public IReadOnlyList<string>? SmsTemplateParamNames { get; set; }
}

/// <summary>
/// 发送选项：不指定渠道时投递给全部启用渠道.
/// </summary>
public class NotificationSendOptions
{
    /// <summary>
    /// 指定投递的渠道 id 集合，空表示全部启用渠道.
    /// </summary>
    public IReadOnlyList<long>? ChannelIds { get; init; }
}

/// <summary>
/// 单渠道发送结果.
/// </summary>
public class NotificationChannelSendResult
{
    /// <summary>
    /// 渠道 id.
    /// </summary>
    public long ChannelId { get; init; }

    /// <summary>
    /// 渠道名称.
    /// </summary>
    public string ChannelName { get; init; } = string.Empty;

    /// <summary>
    /// 是否发送成功.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 失败原因，成功为空.
    /// </summary>
    public string Error { get; init; } = string.Empty;

    /// <summary>
    /// 发送耗时（毫秒）.
    /// </summary>
    public long DurationMs { get; init; }
}
