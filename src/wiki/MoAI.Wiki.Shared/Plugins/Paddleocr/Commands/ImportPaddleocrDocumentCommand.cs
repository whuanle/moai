using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Plugins.Paddleocr.Commands;

/// <summary>
/// 确认导入飞桨 OCR 解析的文档到知识库.
/// </summary>
public class ImportPaddleocrDocumentCommand : IRequest<SimpleInt>, IModelValidator<ImportPaddleocrDocumentCommand>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 文档标题.
    /// </summary>
    public string Title { get; init; } = default!;

    /// <summary>
    /// Markdown 内容.
    /// </summary>
    public string MarkdownContent { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<ImportPaddleocrDocumentCommand> validate)
    {
        validate.RuleFor(x => x.WikiId)
            .GreaterThan(0).WithMessage("知识库 id 不正确");

        validate.RuleFor(x => x.Title)
            .NotEmpty().WithMessage("文档标题不能为空")
            .MaximumLength(200).WithMessage("文档标题不能超过 200 个字符");

        validate.RuleFor(x => x.MarkdownContent)
            .NotEmpty().WithMessage("Markdown 内容不能为空");
    }
}
