using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Prompt.Queries.Responses;

namespace MoAI.Prompt.Queries;

/// <summary>
/// 查询团队提示词列表，仅团队成员可访问.
/// </summary>
public class QueryTeamPromptListCommand : IRequest<QueryPromptListCommandResponse>, IUserIdContext, IModelValidator<QueryTeamPromptListCommand>
{
    /// <summary>
    /// 团队 id.
    /// </summary>
    public int TeamId { get; init; }

    /// <summary>
    /// 按名称/描述关键字过滤，可为空.
    /// </summary>
    public string? Keywords { get; init; }

    /// <summary>
    /// 按分类 id 过滤，为空查全部分类.
    /// </summary>
    public int? PromptClassId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryTeamPromptListCommand> validate)
    {
        validate.RuleFor(x => x.TeamId).GreaterThan(0).WithMessage("团队 id 不正确.");
        validate.RuleFor(x => x.Keywords).MaximumLength(100).WithMessage("关键字最长 100 个字符.");
        validate.RuleFor(x => x.PromptClassId).GreaterThan(0).When(x => x.PromptClassId != null).WithMessage("分类 id 不正确.");
    }
}
