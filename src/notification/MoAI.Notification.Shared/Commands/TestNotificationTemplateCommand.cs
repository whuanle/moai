using FluentValidation;
using MediatR;
using MoAI.Notification.Models;

namespace MoAI.Notification.Commands;

/// <summary>
/// 测试通知模板：按传入变量值渲染模板，返回渲染结果；
/// 指定 channelId 时同步向该渠道真实试发（短信渠道要求模板已映射对应平台模板 Code/Id）.
/// </summary>
public class TestNotificationTemplateCommand : IRequest<TestNotificationTemplateCommandResponse>, IModelValidator<TestNotificationTemplateCommand>
{
    /// <summary>
    /// 模板 id.
    /// </summary>
    public long TemplateId { get; init; }

    /// <summary>
    /// 模板变量值表（需覆盖模板全部变量定义）.
    /// </summary>
    public Dictionary<string, string>? Variables { get; init; }

    /// <summary>
    /// 试发渠道 id，可选；为空时仅返回渲染预览不发送.
    /// </summary>
    public long? ChannelId { get; init; }

    /// <summary>
    /// 动态收件人列表：试发渠道为 SMTP 时为收件人邮箱（必填），短信渠道时为收件人手机号（必填），其它渠道忽略.
    /// </summary>
    public IReadOnlyList<string>? Recipients { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<TestNotificationTemplateCommand> validate)
    {
        validate.RuleFor(x => x.TemplateId).GreaterThan(0).WithMessage("模板 id 不存在");
    }
}

/// <summary>
/// 测试通知模板响应.
/// </summary>
public class TestNotificationTemplateCommandResponse
{
    /// <summary>
    /// 渲染后的主题.
    /// </summary>
    public string RenderedTitle { get; init; } = string.Empty;

    /// <summary>
    /// 渲染后的正文（HTML 模板为 HTML 文本）.
    /// </summary>
    public string RenderedContent { get; init; } = string.Empty;

    /// <summary>
    /// 是否发送成功（未指定渠道时为 false 且 error 为空）.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// 失败原因（发送失败时）.
    /// </summary>
    public string Error { get; init; } = string.Empty;

    /// <summary>
    /// 发送耗时（毫秒），未发送为 0.
    /// </summary>
    public long DurationMs { get; init; }
}
