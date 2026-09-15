using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Prompt.Queries.Responses;

namespace MoAI.Prompt.Queries;

/// <summary>
/// 查询提示词详情（含内容）；个人提示词仅创建人、团队提示词仅团队成员可看，已上架到市场的提示词所有人可看.
/// </summary>
public class QueryPromptCommand : IRequest<QueryPromptCommandResponse>, IUserIdContext, IModelValidator<QueryPromptCommand>
{
    /// <summary>
    /// 提示词 id.
    /// </summary>
    public int PromptId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public long ContextUserId { get; init; }

    /// <inheritdoc/>
    [JsonIgnore]
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QueryPromptCommand> validate)
    {
        validate.RuleFor(x => x.PromptId).GreaterThan(0).WithMessage("提示词 id 不正确.");
    }
}
