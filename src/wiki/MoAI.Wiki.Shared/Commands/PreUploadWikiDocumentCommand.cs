using FluentValidation;
using MediatR;

namespace MoAI.Wiki.Commands;

/// <summary>
/// 预上传知识库文档，生成预签名上传地址.
/// </summary>
public class PreUploadWikiDocumentCommand : IRequest<PreUploadWikiDocumentCommandResponse>, IModelValidator<PreUploadWikiDocumentCommand>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 文件名称.
    /// </summary>
    public string FileName { get; init; } = default!;

    /// <summary>
    /// 文件类型 (MIME Type).
    /// </summary>
    public string ContentType { get; init; } = default!;

    /// <summary>
    /// 文件大小（字节）.
    /// </summary>
    public int FileSize { get; init; }

    /// <summary>
    /// 文件 SHA-256.
    /// </summary>
    public string SHA256 { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<PreUploadWikiDocumentCommand> validate)
    {
        // WikiId 来自路由参数由 Controller 回填，自动验证发生在回填前，此处不做校验（否则恒 400）
        validate.RuleFor(x => x.FileName).NotEmpty().WithMessage("文件名称不能为空.").MaximumLength(255).WithMessage("文件名称最长 255 个字符.");
        validate.RuleFor(x => x.ContentType).NotEmpty().WithMessage("文件类型不能为空.");
        validate.RuleFor(x => x.FileSize).GreaterThan(0).WithMessage("文件大小必须大于 0.").LessThan(1024 * 1024 * 1024).WithMessage("文件大小不能超过 1GB.");
        validate.RuleFor(x => x.SHA256).NotEmpty().WithMessage("文件 SHA256 不能为空.");
    }
}
