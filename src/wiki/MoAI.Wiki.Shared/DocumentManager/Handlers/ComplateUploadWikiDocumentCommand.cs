using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.DocumentManager.Handlers;

/// <summary>
/// 结束上传文件.
/// </summary>
public class ComplateUploadWikiDocumentCommand : IRequest<EmptyCommandResponse>, IModelValidator<ComplateUploadWikiDocumentCommand>
{
    /// <summary>
    /// 知识库ID.
    /// </summary>
    public int WikiId { get; init; } = default!;

    /// <summary>
    /// 上传成功或失败.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 文件ID.
    /// </summary>
    public int FileId { get; set; }

    /// <summary>
    /// 文件名称.
    /// </summary>
    public string FileName { get; set; } = default!;

    /// <inheritdoc/>
    public void Validate(AbstractValidator<ComplateUploadWikiDocumentCommand> validate)
    {
        validate.RuleFor(x => x.WikiId)
            .NotEmpty().WithMessage("知识库id不正确")
            .GreaterThan(0).WithMessage("知识库id不正确");

        validate.RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("文件名称长度在 2-100 之间.")
            .Length(2, 100).WithMessage("文件名称长度在 2-100 之间.");
    }
}