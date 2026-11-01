using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Message.Models;
using MoAI.Message.Queries.Responses;

namespace MoAI.Message.Queries;

/// <summary>
/// 分页查询我的站内信，按创建时间倒序.
/// </summary>
public class QueryMessageListCommand : PagedParamter, IRequest<QueryMessageListCommandResponse>, IModelValidator<QueryMessageListCommand>, IUserIdContext
{
    /// <summary>
    /// 按消息类型过滤，空为全部.
    /// </summary>
    public MessageType? MessageType { get; init; }

    /// <summary>
    /// 按已读状态过滤，空为全部.
    /// </summary>
    public bool? IsRead { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryMessageListCommand> validate)
    {
        validate.RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("每页数量 1~100.");
    }
}
