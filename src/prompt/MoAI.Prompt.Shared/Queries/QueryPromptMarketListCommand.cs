using FluentValidation;
using MediatR;
using MoAI.Prompt.Queries.Responses;

namespace MoAI.Prompt.Queries;

/// <summary>
/// 查询提示词市场列表（已上架 is_public=true 的提示词），所有登录用户可访问.
/// </summary>
public class QueryPromptMarketListCommand : IRequest<QueryPromptListCommandResponse>, IModelValidator<QueryPromptMarketListCommand>
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
    public static void Validate(AbstractValidator<QueryPromptMarketListCommand> validate)
    {
        validate.RuleFor(x => x.Keywords).MaximumLength(100).WithMessage("关键字最长 100 个字符.");
        validate.RuleFor(x => x.PromptClassId).GreaterThan(0).When(x => x.PromptClassId != null).WithMessage("分类 id 不正确.");
    }
}
