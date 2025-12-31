using FluentValidation;
using MediatR;
using MoAI.App.AIAssistant.Commands.Responses;

namespace MoAI.App.AIAssistant.Commands.File;

/// <summary>
/// 预上传对话文件.
/// </summary>
public class PreUploadChatFileDocumentCommand : IRequest<PreUploadChatFileDocumentCommandResponse>, IModelValidator<PreUploadChatFileDocumentCommand>
{
    /// <summary>
    ///  知识库 id.
    /// </summary>
    public Guid ChatId { get; init; } = default!;

    /// <summary>
    /// 文件名称.
    /// </summary>
    public string FileName { get; set; } = default!;

    /// <summary>
    /// 文件类型.
    /// </summary>
    public string ContentType { get; set; } = default!;

    /// <summary>
    /// 文件大小.
    /// </summary>
    public int FileSize { get; set; } = default!;

    /// <summary>
    /// 文件 MD5.
    /// </summary>
    public string MD5 { get; set; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<PreUploadChatFileDocumentCommand> validate)
    {
        validate.RuleFor(x => x.ChatId)
            .NotEmpty().WithMessage("对话id错误");

        validate.RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("文件类型不正确.")
            .Length(2, 50).WithMessage("文件类型不正确.");

        validate.RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("文件名称长度在 2-100 之间.")
            .Length(2, 100).WithMessage("文件名称长度在 2-100 之间.");

        validate.RuleFor(x => x.MD5)
            .NotEmpty().WithMessage("文件 MD5 不能为空.")
            .Length(32).WithMessage("文件 MD5 长度必须为 32 位.")
            .Matches("^[a-fA-F0-9]{32}$").WithMessage("文件 MD5 格式不正确.");

        validate.RuleFor(x => x.FileSize)
            .Must(x => x < 1024 * 1024 * 1024).WithMessage("文件大小不能超过 1GB.");
    }
}
