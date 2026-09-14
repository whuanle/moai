using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.App.Queries.Responses;
using MoAI.Infra.Models;
using MoAI.Infra.Services;

namespace MoAI.App.Queries;

/// <summary>
/// 分页查询应用对话日志（该应用下全部用户的正式会话，压缩后视图）；需要团队 Admin 及以上角色.
/// </summary>
public class QueryAppLogsCommand : PagedParamter, IRequest<QueryAppLogsCommandResponse>, IUserIdContext, IModelValidator<QueryAppLogsCommand>
{
    /// <summary>应用 id（来自路由）.</summary>
    public Guid AppId { get; init; }

    /// <summary>标题关键字，模糊匹配；可为空.</summary>
    public string? Keyword { get; init; }

    /// <summary>按会话用户类型过滤；为空不过滤.</summary>
    public UserType? UserType { get; init; }

    /// <summary>最后消息时间下界（含）；可为空.</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>最后消息时间上界（含）；可为空.</summary>
    public DateTimeOffset? To { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryAppLogsCommand> validate)
    {
        validate.RuleFor(x => x.Keyword).MaximumLength(50).WithMessage("标题关键字最长 50 个字符.");
    }
}
