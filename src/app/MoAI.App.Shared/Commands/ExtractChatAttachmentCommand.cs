using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.App.Commands;

/// <summary>
/// 对话附件文本提取命令：对已上传到公开 chat 目录的文档做 Maomi.ToMarkdown 提取，
/// 结果由前端拼进用户消息文本发送给模型（AG-UI 对话链路为纯文本）.
/// </summary>
public class ExtractChatAttachmentCommand : IRequest<ExtractChatAttachmentResponse>, IModelValidator<ExtractChatAttachmentCommand>
{
    /// <summary>
    /// 存储对象 key（必须位于 public/chat 目录，避免越权读取私有文件）.
    /// </summary>
    public string ObjectKey { get; init; } = string.Empty;

    /// <summary>
    /// 原始文件名（带扩展名，提取器按扩展名选择解析器）.
    /// </summary>
    public string FileName { get; init; } = string.Empty;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<ExtractChatAttachmentCommand> validate)
    {
        validate.RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("文件名不能为空.")
            .MaximumLength(255).WithMessage("文件名过长.");

        validate.RuleFor(x => x.ObjectKey)
            .NotEmpty().WithMessage("附件未上传.")
            .MaximumLength(512).WithMessage("附件标识过长.")
            .Must(key => key.StartsWith("public/chat/", StringComparison.OrdinalIgnoreCase))
            .WithMessage("仅支持对话附件目录的文件.");
    }
}

/// <summary>
/// 附件提取结果.
/// </summary>
public class ExtractChatAttachmentResponse
{
    /// <summary>
    /// 提取出的 markdown 文本（超长时截断并附加截断标记）.
    /// </summary>
    public string Markdown { get; init; } = string.Empty;

    /// <summary>
    /// 提取文本长度（截断前）.
    /// </summary>
    public int ContentLength { get; init; }

    /// <summary>
    /// 是否发生截断.
    /// </summary>
    public bool Truncated { get; init; }
}
