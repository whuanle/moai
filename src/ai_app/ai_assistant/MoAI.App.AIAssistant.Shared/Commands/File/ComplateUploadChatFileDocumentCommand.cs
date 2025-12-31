using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Documents.Handlers;

/// <summary>
/// 结束上传文件.
/// </summary>
public class ComplateUploadChatFileDocumentCommand : IRequest<ComplateUploadChatFileDocumentCommandResponse>, IModelValidator<ComplateUploadChatFileDocumentCommand>
{
    /// <summary>
    ///  知识库 id.
    /// </summary>
    public Guid ChatId { get; init; } = default!;

    /// <summary>
    /// 上传成功或失败.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// 文件key.
    /// </summary>
    public int FileId { get; set; }

    /// <summary>
    /// 文件名称.
    /// </summary>
    public string FileName { get; set; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<ComplateUploadChatFileDocumentCommand> validate)
    {
        validate.RuleFor(x => x.ChatId)
            .NotEmpty().WithMessage("对话id错误");

        validate.RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("文件名称长度在 2-100 之间.")
            .Length(2, 100).WithMessage("文件名称长度在 2-100 之间.");
    }
}

public class ComplateUploadChatFileDocumentCommandResponse
{
    /// <summary>
    /// 文件下载或图片预览地址，有效期 3 小时.
    /// </summary>
    public Uri ViewUrl { get; init; } = default!;
}