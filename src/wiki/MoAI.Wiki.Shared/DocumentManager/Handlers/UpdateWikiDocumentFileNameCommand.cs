using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.DocumentManager.Handlers;
/// <summary>
/// 修改知识库文档名称命令
/// </summary>
public class UpdateWikiDocumentFileNameCommand : IRequest<EmptyCommandResponse>, IModelValidator<UpdateWikiDocumentFileNameCommand>
{
    /// <summary>
    /// 知识库ID
    /// </summary>
    public int WikiId { get; set; }

    /// <summary>
    /// 文档ID
    /// </summary>
    public int DocumentId { get; set; }

    /// <summary>
    /// 新的文件名称
    /// </summary>
    public string FileName { get; set; } = default!;

    /// <inheritdoc/>
    public void Validate(AbstractValidator<UpdateWikiDocumentFileNameCommand> validate)
    {
        validate.RuleFor(x => x.WikiId).NotEmpty();
        validate.RuleFor(x => x.DocumentId).NotEmpty();
        validate.RuleFor(x => x.FileName).NotEmpty().MaximumLength(100)
            .WithMessage("文件名称不能为空且长度不能超过100个字符。");
    }
}