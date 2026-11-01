using System.ComponentModel;
using FluentValidation;
using MediatR;
using MoAI.Database.Enums;
using MoAI.Infra.Models;

namespace MoAI.Notification.Queries;

/// <summary>
/// 查询通知渠道列表（全量，支持名称模糊搜索）.
/// </summary>
public class QueryNotificationChannelsCommand : IRequest<QueryNotificationChannelsCommandResponse>, IModelValidator<QueryNotificationChannelsCommand>
{
    /// <summary>
    /// 按渠道名称模糊搜索，可选.
    /// </summary>
    public string? Search { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryNotificationChannelsCommand> validate)
    {
        validate.RuleFor(x => x.Search).MaximumLength(100).WithMessage("搜索关键字长度不能超过 100");
    }
}

/// <summary>
/// 通知渠道列表响应.
/// </summary>
public class QueryNotificationChannelsCommandResponse
{
    /// <summary>
    /// 渠道列表.
    /// </summary>
    public IReadOnlyList<QueryNotificationChannelsCommandResponseItem> Items { get; init; } = Array.Empty<QueryNotificationChannelsCommandResponseItem>();
}

/// <summary>
/// 通知渠道项.
/// </summary>
public class QueryNotificationChannelsCommandResponseItem : AuditsInfo
{
    /// <summary>
    /// 渠道 id.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// 渠道名称.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// 渠道类型.
    /// </summary>
    public NotificationChannelType Type { get; init; }

    /// <summary>
    /// 渠道配置 JSON.
    /// </summary>
    public string Config { get; init; } = string.Empty;

    /// <summary>
    /// 是否启用.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// 渠道描述.
    /// </summary>
    public string Description { get; init; } = string.Empty;
}
