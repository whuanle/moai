using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Prompt.Queries.Responses;

namespace MoAI.Prompt.Queries;

/// <summary>
/// 查询使用次数最多的专家提示词（本人个人提示词 + 指定团队提示词范围，按使用次数倒序），
/// 供对话页输入框下方推荐展示.
/// </summary>
public class QueryTopUsedPromptsCommand : IRequest<QueryPromptListCommandResponse>, IUserIdContext, IModelValidator<QueryTopUsedPromptsCommand>
{
    /// <summary>
    /// 团队 id，团队提示词仅统计该团队.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 返回条数，默认且最大 10.
    /// </summary>
    public int Limit { get; init; } = 10;

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryTopUsedPromptsCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
        validate.RuleFor(x => x.Limit).InclusiveBetween(1, 10).WithMessage("返回条数须在 1~10 之间.");
    }
}
