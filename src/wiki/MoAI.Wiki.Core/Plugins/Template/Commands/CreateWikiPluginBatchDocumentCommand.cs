using FluentValidation;
using MediatR;
using MoAI.AI.Models;
using MoAI.Wiki.Batch.Commands;
using MoAI.Wiki.DocumentEmbedding.Commands;

namespace MoAI.Wiki.Plugins.Template.Commands;

public class CreateWikiPluginBatchDocumentCommand : IModelValidator<CreateWikiPluginBatchDocumentCommand>
{
    /// <summary>
    /// 文档 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 具体是那个配置.
    /// </summary>
    public int ConfigId { get; init; }

    /// <summary>
    /// 普通切割，跟 AI 切割二选一.
    /// </summary>
    public WikiDocumentTextPartitionPreviewCommand? Partion { get; init; }

    /// <summary>
    /// AI 切割，跟 普通切割二选一.
    /// </summary>
    public WikiDocumentAiTextPartionCommand? AiPartion { get; init; }

    /// <summary>
    /// 使用何种文档处理策略生成元数据，如果不填写则不生成.
    /// </summary>
    public IReadOnlyCollection<PreprocessStrategyType> PreprocessStrategyType { get; init; } = Array.Empty<PreprocessStrategyType>();

    /// <summary>
    /// 是否将 chunk 源文本也向量化.
    /// </summary>
    public bool IsEmbedSourceText { get; init; } = true;

    /// <summary>
    /// 并发线程数量.
    /// </summary>
    public int? ThreadCount { get; init; } = 5;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CreateWikiPluginBatchDocumentCommand> validate)
    {
        validate.RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        validate.RuleFor(x => x.ConfigId)
            .NotEmpty().WithMessage("配置id不正确");

        validate.When(x => x.Partion is not null, () =>
        {
            validate.RuleFor(x => x.AiPartion)
                .Null().WithMessage("普通切割和AI切割只能选择其一");
        });

        validate.When(x => x.AiPartion is not null, () =>
        {
            validate.RuleFor(x => x.Partion)
                .Null().WithMessage("普通切割和AI切割只能选择其一");
        });

        validate.When(x => x.ThreadCount.HasValue, () =>
        {
            validate.RuleFor(x => x.ThreadCount.Value)
                .GreaterThan(0).WithMessage("线程数量必须大于0");
        });
    }
}
