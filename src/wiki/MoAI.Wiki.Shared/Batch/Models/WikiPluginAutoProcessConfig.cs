using FluentValidation;
using MediatR;
using MoAI.AI.Models;
using MoAI.Infra.Models;
using MoAI.Wiki.DocumentEmbedding.Commands;

namespace MoAI.Wiki.Batch.Models;

/// <summary>
/// 批处理任务配置.
/// </summary>
public class WikiPluginAutoProcessConfig : IModelValidator<WikiPluginAutoProcessConfig>
{
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
    /// 策略化Ai 模型，如果不填写则不使用.
    /// </summary>
    public int? PreprocessStrategyAiModel { get; init; } = 0;

    /// <summary>
    /// 是否自动向量化，如果不启用，则不需要填写 IsEmbedSourceText、ThreadCount。
    /// </summary>
    public bool? IsEmbedding { get; init; } = false;

    /// <summary>
    /// 是否将 chunk 源文本也向量化.
    /// </summary>
    public bool? IsEmbedSourceText { get; init; } = false;

    /// <summary>
    /// 并发线程数量，不需要向量化的时候不需要填写.
    /// </summary>
    public int? ThreadCount { get; init; } = 5;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<WikiPluginAutoProcessConfig> validate)
    {
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

        validate.RuleFor(x => x.ThreadCount)
            .GreaterThan(0).WithMessage("线程数量必须大于0")
            .LessThan(10).WithMessage("最大线程数量10");
    }
}
