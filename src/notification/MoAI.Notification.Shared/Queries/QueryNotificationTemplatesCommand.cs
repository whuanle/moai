using FluentValidation;
using MediatR;
using MoAI.Database.Enums;
using MoAI.Infra.Models;
using MoAI.Notification.Models;

namespace MoAI.Notification.Queries;

/// <summary>
/// 查询通知模板列表（全量，支持名称模糊搜索）.
/// </summary>
public class QueryNotificationTemplatesCommand : IRequest<QueryNotificationTemplatesCommandResponse>, IModelValidator<QueryNotificationTemplatesCommand>
{
    /// <summary>
    /// 按模板名称模糊搜索，可选.
    /// </summary>
    public string? Search { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryNotificationTemplatesCommand> validate)
    {
        validate.RuleFor(x => x.Search).MaximumLength(100).WithMessage("搜索关键字长度不能超过 100");
    }
}

/// <summary>
/// 通知模板列表响应.
/// </summary>
public class QueryNotificationTemplatesCommandResponse
{
    /// <summary>
    /// 模板列表.
    /// </summary>
    public IReadOnlyList<QueryNotificationTemplatesCommandResponseItem> Items { get; init; } = Array.Empty<QueryNotificationTemplatesCommandResponseItem>();
}

/// <summary>
/// 通知模板项.
/// </summary>
public class QueryNotificationTemplatesCommandResponseItem : AuditsInfo
{
    /// <summary>
    /// 模板 id.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// 是否平台内置模板（内置模板不可删除、可编辑）.
    /// </summary>
    public bool IsBuiltIn { get; init; }

    /// <summary>
    /// 模板名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 通知主题模板.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// 通知正文模板.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// 内容类型.
    /// </summary>
    public NotificationContentType ContentType { get; init; }

    /// <summary>
    /// 变量定义 JSON.
    /// </summary>
    public string Variables { get; init; } = string.Empty;

    /// <summary>
    /// 阿里云短信模板 Code.
    /// </summary>
    public string AliyunTemplateCode { get; init; } = string.Empty;

    /// <summary>
    /// 腾讯云短信模板 Id.
    /// </summary>
    public string TencentTemplateId { get; init; } = string.Empty;

    /// <summary>
    /// 模板描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;
}
