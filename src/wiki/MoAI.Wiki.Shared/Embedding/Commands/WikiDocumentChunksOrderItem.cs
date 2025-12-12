using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.DocumentEmbedding.Models;

namespace MoAI.Wiki.DocumentEmbedding.Commands;

/// <summary>
/// 切片排序.
/// </summary>
public class WikiDocumentChunksOrderItem : IModelValidator<WikiDocumentChunksOrderItem>
{
    /// <summary>
    /// 切片ID.
    /// </summary>
    public long ChunkId { get; set; }

    /// <summary>
    /// 切片排序.
    /// </summary>
    public int Order { get; set; }

    /// <inheritdoc/>
    public void Validate(AbstractValidator<WikiDocumentChunksOrderItem> validate)
    {
        validate.RuleFor(x => x.ChunkId).GreaterThan(0).WithMessage("切片ID错误");
        validate.RuleFor(x => x.Order).GreaterThan(0).WithMessage("切片排序错误");
    }
}