using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Wikis.Queries.Response;

namespace MoAI.Wiki.Documents.Queries;

/// <summary>
/// 搜索知识库文档分片.
/// </summary>
public class SearchWikiDocumentTextCommand : IRequest<SearchWikiDocumentTextCommandResponse>, IModelValidator<SearchWikiDocumentTextCommand>, IUserIdContext
{
    /// <summary>
    ///  知识库 id.
    /// </summary>
    public int WikiId { get; init; } = default!;

    /// <summary>
    /// 文档id，不设置时搜索整个知识库.
    /// </summary>
    public int? DocumentId { get; set; }

    /// <summary>
    /// 提问.
    /// </summary>
    public string Query { get; init; } = string.Empty;

    /// <summary>
    /// 最小相关度，范围0.0-1.0，默认0表示不过滤.
    /// </summary>
    public double MinRelevance { get; init; }

    /// <summary>
    /// 最大召回数量，但是最后聚合结果可能小于该值.
    /// </summary>
    public int Limit { get; init; } = 30;

    /// <summary>
    /// 要使用的 ai 模型，如果 IsOptimizeQuery 或 IsAnswer = true，必须设置.
    /// </summary>
    public int AiModelId { get; init; }

    /// <summary>
    /// 优化提问.
    /// </summary>
    public bool IsOptimizeQuery { get; init; }

    /// <summary>
    /// 是否需要 ai 回答.
    /// </summary>
    public bool IsAnswer { get; init; }

    /// <inheritdoc/>
    public int ContextUserId { get; init; }

    /// <inheritdoc/>
    public UserType ContextUserType { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<SearchWikiDocumentTextCommand> validate)
    {
        validate.RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        validate.RuleFor(x => x.Query).NotEmpty();

        validate.RuleFor(x => x.Limit)
            .GreaterThan(0).WithMessage("召回数量必须大于0")
            .LessThanOrEqualTo(100).WithMessage("召回数量不能超过100");

        validate.RuleFor(x => x.MinRelevance)
            .GreaterThanOrEqualTo(0).WithMessage("最小相关度必须大于等于0")
            .LessThanOrEqualTo(1).WithMessage("最小相关度必须小于等于1");

        validate.When(x => x.IsOptimizeQuery || x.IsAnswer, () =>
        {
            validate.RuleFor(x => x.AiModelId)
                .NotEmpty().WithMessage("Ai模型id不正确")
                .GreaterThan(0).WithMessage("Ai模型id不正确");
        });
    }
}
