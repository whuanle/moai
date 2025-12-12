using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.DocumentEmbedding.Models;
using MoAI.Wiki.Embeddings.Models;

namespace MoAI.Wiki.DocumentEmbedding.Commands;

/// <summary>
/// 切割知识库文档.
/// </summary>
public class WikiDocumentTextPartitionPreviewCommand : DocumentTextPartionConfig, IRequest<WikiDocumentTextPartitionPreviewCommandResponse>, IModelValidator<WikiDocumentTextPartitionPreviewCommand>
{
    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int DocumentId { get; init; }

    /// <inheritdoc/>
    public void Validate(AbstractValidator<WikiDocumentTextPartitionPreviewCommand> validate)
    {
        validate.RuleFor(x => x.WikiId).GreaterThan(0).WithMessage("知识库id必须大于0");
        validate.RuleFor(x => x.DocumentId).GreaterThan(0).WithMessage("文档id必须大于0");
        validate.RuleFor(x => x.MaxTokensPerChunk).GreaterThan(0).WithMessage("每个分块的最大 token 数必须大于0");
        validate.RuleFor(x => x.Overlap).GreaterThanOrEqualTo(0).WithMessage("重叠数量必须大于等于0");
    }
}
