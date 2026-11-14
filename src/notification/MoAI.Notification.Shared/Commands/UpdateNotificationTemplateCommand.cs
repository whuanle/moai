using FluentValidation;
using MediatR;
using MoAI.Database.Enums;
using MoAI.Infra.Models;
using MoAI.Notification.Models;

namespace MoAI.Notification.Commands;

/// <summary>
/// 更新通知模板（整体覆盖）.
/// </summary>
public class UpdateNotificationTemplateCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateNotificationTemplateCommand>
{
    /// <summary>
    /// 模板 id（路由传入）.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 模板名称（唯一）.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 通知主题模板（邮件主题用，短信无主题；可含 {{变量}} 双花括号占位符）.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 通知正文模板（纯文本或 HTML，含 {{变量}} 双花括号占位符，与云邮件平台提审形态一致）.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// 内容类型.
    /// </summary>
    public NotificationContentType ContentType { get; init; } = NotificationContentType.Text;

    /// <summary>
    /// 变量定义 JSON（[{"name","description","example"}]，空表示无变量）.
    /// </summary>
    public string Variables { get; init; } = string.Empty;

    /// <summary>
    /// 阿里云短信模板 Code（平台审核通过后回填，空表示不用于阿里云短信）.
    /// </summary>
    public string AliyunTemplateCode { get; init; } = string.Empty;

    /// <summary>
    /// 腾讯云短信模板 Id（平台审核通过后回填，空表示不用于腾讯云短信）.
    /// </summary>
    public string TencentTemplateId { get; init; } = string.Empty;

    /// <summary>
    /// 模板描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<UpdateNotificationTemplateCommand> validate)
    {
        // Id 由路由传入（Controller 赋值，晚于模型验证），存在性由 Handler 校验 404
        validate.RuleFor(x => x.Name).NotEmpty().MaximumLength(100).WithMessage("模板名称不能为空且长度不超过 100");
        validate.RuleFor(x => x.Title).MaximumLength(256).WithMessage("模板主题长度不能超过 256");
        validate.RuleFor(x => x.Content).NotEmpty().MaximumLength(10000).WithMessage("模板正文不能为空且长度不超过 10000");
        validate.RuleFor(x => x.Variables).MaximumLength(4000).WithMessage("变量定义长度不能超过 4000");
        validate.RuleFor(x => x.AliyunTemplateCode).MaximumLength(128).WithMessage("阿里云模板 Code 长度不能超过 128");
        validate.RuleFor(x => x.TencentTemplateId).MaximumLength(128).WithMessage("腾讯云模板 Id 长度不能超过 128");
        validate.RuleFor(x => x.Description).MaximumLength(500).WithMessage("模板描述长度不能超过 500");
        validate.RuleFor(x => x.Variables)
            .Must((req, variables) => NotificationTemplateVariableParser.Parse(variables, out _) != null)
            .WithMessage("变量定义不是有效的 JSON 数组或变量名不合法");
    }
}
