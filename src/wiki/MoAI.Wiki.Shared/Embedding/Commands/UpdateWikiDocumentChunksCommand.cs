using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.DocumentEmbedding.Models;

namespace MoAI.Wiki.DocumentEmbedding.Commands;

/// <summary>
/// 对需要更新的切片块进行更新.
/// </summary>
public class UpdateWikiDocumentChunksCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateWikiDocumentChunksCommand>
{
    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int DocumentId { get; init; }

    /// <summary>
    /// 切片列表.
    /// </summary>
    public IReadOnlyCollection<WikiDocumenChunkItem> Chunks { get; init; } = Array.Empty<WikiDocumenChunkItem>();

    /// <inheritdoc/>
    public void Validate(AbstractValidator<UpdateWikiDocumentChunksCommand> validate)
    {
        validate.RuleFor(x => x.WikiId).GreaterThan(0).WithMessage("知识库ID错误");
        validate.RuleFor(x => x.DocumentId).GreaterThan(0).WithMessage("文档ID错误");
        validate.RuleFor(x => x.Chunks).Must(x => x.Count > 0).WithMessage("未提交任何切片");
    }
}
