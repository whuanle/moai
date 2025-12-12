using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Embedding.Commands;

/// <summary>
/// 删除文档的一个块.
/// </summary>
public class DeleteWikiDocumentChunkCommand : IRequest<EmptyCommandResponse>, IModelValidator<DeleteWikiDocumentChunkCommand>
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
    /// 切片ID.
    /// </summary>
    public long ChunkId { get; set; }

    /// <inheritdoc/>
    public void Validate(AbstractValidator<DeleteWikiDocumentChunkCommand> validate)
    {
        validate.RuleFor(x => x.WikiId).GreaterThan(0).WithMessage("知识库ID错误");
        validate.RuleFor(x => x.DocumentId).GreaterThan(0).WithMessage("文档ID错误");
        validate.RuleFor(x => x.ChunkId).GreaterThan(0).WithMessage("切片id错误");
    }
}
