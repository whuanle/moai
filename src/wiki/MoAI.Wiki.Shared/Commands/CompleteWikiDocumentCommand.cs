using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Commands;

/// <summary>
/// 完成知识库文档上传.
/// </summary>
public class CompleteWikiDocumentCommand : IRequest<EmptyCommandResponse>, IModelValidator<CompleteWikiDocumentCommand>
{
    /// <summary>
    /// 知识库 id.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 上传成功或失败.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// 文件 id.
    /// </summary>
    public long FileId { get; init; }

    /// <summary>
    /// 文件名称.
    /// </summary>
    public string FileName { get; init; } = default!;

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<CompleteWikiDocumentCommand> validate)
    {
        // WikiId 来自路由参数由 Controller 回填，自动验证发生在回填前，此处不做校验（否则恒 400）
        validate.RuleFor(x => x.FileId).GreaterThan(0).WithMessage("文件 id 不正确.");
        validate.RuleFor(x => x.FileName).NotEmpty().WithMessage("文件名称不能为空.").MaximumLength(255).WithMessage("文件名称最长 255 个字符.");
    }
}
