using FastEndpoints;
using FluentValidation;
using MoAI.Wiki.DocumentManager.Handlers;

namespace MoAI.Wiki.Documents.Validators;
/// <summary>
/// 修改知识库文档名称命令验证器
/// </summary>
public class UpdateWikiDocumentFileNameCommandValidator : AbstractValidator<UpdateWikiDocumentFileNameCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateWikiDocumentFileNameCommandValidator"/> class.
    /// </summary>
    public UpdateWikiDocumentFileNameCommandValidator()
    {
        RuleFor(x => x.WikiId).NotEmpty();
        RuleFor(x => x.DocumentId).NotEmpty();
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(100)
            .WithMessage("文件名称不能为空且长度不能超过100个字符。");
    }
}
