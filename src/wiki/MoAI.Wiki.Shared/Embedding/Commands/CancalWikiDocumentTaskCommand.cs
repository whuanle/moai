using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Embedding.Commands;

/// <summary>
/// 取消文档处理任务.
/// </summary>
public class CancalWikiDocumentTaskCommand : IRequest<EmptyCommandResponse>, IModelValidator<CancalWikiDocumentTaskCommand>
{
    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; set; }

    /// <summary>
    /// 文档id.
    /// </summary>
    public int DocumentId { get; set; }

    /// <summary>
    /// 任务 id.
    /// </summary>
    public Guid TaskId { get; set; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CancalWikiDocumentTaskCommand> validate)
    {
        validate.RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        validate.RuleFor(x => x.DocumentId)
            .NotEmpty().WithMessage("文档id不正确")
            .GreaterThan(0).WithMessage("文档id不正确");
    }
}