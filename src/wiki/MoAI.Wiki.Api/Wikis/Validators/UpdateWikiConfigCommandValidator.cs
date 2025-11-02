using FastEndpoints;
using FluentValidation;
using MoAI.Wiki.Wikis.Commands;

namespace MoAI.Wiki.Wikis.Validators;

/// <summary>
/// UpdateWikiConfigCommandValidator
/// </summary>
public class UpdateWikiConfigCommandValidator : Validator<UpdateWikiConfigCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateWikiConfigCommandValidator"/> class.
    /// </summary>
    public UpdateWikiConfigCommandValidator()
    {
        RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        RuleFor(x => x.EmbeddingModelId)
            .NotEmpty().WithMessage("模型id不正确")
            .GreaterThan(0).WithMessage("模型id不正确");

        RuleFor(x => x.EmbeddingDimensions)
            .NotEmpty().WithMessage("向量维度配置不正确")
            .GreaterThan(0).WithMessage("向量维度配置不正确");

        RuleFor(x => x.EmbeddingBatchSize)
            .NotEmpty().WithMessage("批处理大小配置不正确")
            .GreaterThan(0).WithMessage("批处理大小配置不正确");

        RuleFor(x => x.MaxRetries)
            .NotEmpty().WithMessage("文档处理最大重试次数0-5")
            .GreaterThan(0).WithMessage("文档处理最大重试次数0-5")
            .LessThan(5).WithMessage("文档处理最大重试次数0-5");
    }
}
