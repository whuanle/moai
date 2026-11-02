using FluentValidation;
using MediatR;
using MoAI.Database.Enums;
using MoAI.Infra.Models;
using MoAI.Notification.Models;

namespace MoAI.Notification.Commands;

/// <summary>
/// 测试通知渠道：向目标渠道同步发送一条测试通知并返回结果（不落发送记录）.
/// 指定 id 时使用已保存配置（忽略启用状态）；否则使用传入的类型与配置（保存前试发）.
/// SMTP 渠道须传入收件人邮箱、短信渠道须传入收件人手机号（发送时动态指定）；
/// 应用机器人渠道（feishuAppBot/dingtalkAppBot/wecomAppBot）测试仅定向当前管理员本人（按其平台账号绑定投递）.
/// </summary>
public class TestNotificationChannelCommand : IRequest<TestNotificationChannelCommandResponse>, IModelValidator<TestNotificationChannelCommand>, IUserIdContext
{
    /// <summary>
    /// 已保存渠道 id，空表示使用传入配置试发.
    /// </summary>
    public long? Id { get; init; }

    /// <summary>
    /// 渠道类型（id 为空时必填）.
    /// </summary>
    public NotificationChannelType? Type { get; init; }

    /// <summary>
    /// 渠道配置 JSON（id 为空时必填）.
    /// </summary>
    public string? Config { get; init; }

    /// <summary>
    /// 动态收件人列表：SMTP 渠道为收件人邮箱（必填），短信渠道为收件人手机号（必填），其它渠道忽略.
    /// </summary>
    public IReadOnlyList<string>? Recipients { get; init; }

    /// <inheritdoc/>
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<TestNotificationChannelCommand> validate)
    {
        validate.RuleFor(x => x)
            .Must(x => x.Id > 0 || NotificationChannelConfigs.Validate(x.Type.GetValueOrDefault(), x.Config) == null)
            .WithMessage("缺少渠道 id，或渠道配置与所选类型不匹配");
    }
}

/// <summary>
/// 测试通知渠道响应.
/// </summary>
public class TestNotificationChannelCommandResponse
{
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
