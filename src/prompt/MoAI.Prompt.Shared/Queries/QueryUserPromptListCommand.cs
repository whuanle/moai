using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Prompt.Queries.Responses;

namespace MoAI.Prompt.Queries;

/// <summary>
/// 查询我的个人提示词列表（TeamId=0 且本人创建），仅本人可见.
/// </summary>
public class QueryUserPromptListCommand : IRequest<QueryPromptListCommandResponse>, IUserIdContext, IModelValidator<QueryUserPromptListCommand>
{
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
    public static void Validate(AbstractValidator<QueryUserPromptListCommand> validate)
    {
        validate.RuleFor(x => x.Keywords).MaximumLength(100).WithMessage("关键字最长 100 个字符.");
        validate.RuleFor(x => x.PromptClassId).GreaterThan(0).When(x => x.PromptClassId != null).WithMessage("分类 id 不正确.");
    }
}
