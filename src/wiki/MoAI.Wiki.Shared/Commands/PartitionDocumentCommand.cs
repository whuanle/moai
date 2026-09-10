using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.Wiki.Models;

namespace MoAI.Wiki.Commands;

/// <summary>
/// 普通切割知识库文档（Maomi.ToMarkdown 多模式切分），仅团队成员可操作.
/// 需先执行 <see cref="ExtractDocumentContentCommand"/> 提取内容，之后才能切割。
/// </summary>
public class PartitionDocumentCommand : IRequest<EmptyCommandResponse>, IModelValidator<PartitionDocumentCommand>
{
    /// <summary>
    /// 知识库 id，由 Controller 从路由参数回填.
    /// </summary>
    public long WikiId { get; init; }

    /// <summary>
    /// 文档 id，由 Controller 从路由参数回填.
    /// </summary>
    public long DocumentId { get; init; }

    /// <summary>
    /// 切割模式.
    /// </summary>
    public DocumentPartitionSplitMode SplitMode { get; init; } = DocumentPartitionSplitMode.Markdown;

    /// <summary>
    /// 切片大小（1-8192，单位由 <see cref="SizeUnit"/> 决定）.
    /// </summary>
    public int ChunkSize { get; init; }

    /// <summary>
    /// 切片重叠大小（0-8192，单位由 <see cref="OverlapUnit"/> 决定）.
    /// </summary>
    public int ChunkOverlap { get; init; }

    /// <summary>
    /// 重叠单位.
    /// </summary>
    public DocumentPartitionOverlapUnit OverlapUnit { get; init; } = DocumentPartitionOverlapUnit.Character;

    /// <summary>
    /// 切片大小计量单位.
    /// </summary>
    public DocumentPartitionSizeUnit SizeUnit { get; init; } = DocumentPartitionSizeUnit.Character;

    /// <summary>
    /// Token 计量时使用的编码名或模型名，为空默认 cl100k_base.
    /// </summary>
    public string? TokenEncodingOrModel { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<PartitionDocumentCommand> validate)
    {
        validate.RuleFor(x => x.ChunkSize)
            .GreaterThan(0).WithMessage("切片大小必须大于 0.")
            .LessThanOrEqualTo(8192).WithMessage("切片大小不能超过 8192.");
        validate.RuleFor(x => x.ChunkOverlap)
            .GreaterThanOrEqualTo(0).WithMessage("切片重叠不能小于 0.")
            .LessThanOrEqualTo(8192).WithMessage("切片重叠不能超过 8192.");
        validate.RuleFor(x => x.ChunkOverlap)
            .LessThan(x => x.ChunkSize).WithMessage("按字符重叠时，切片重叠必须小于切片大小.")
            .When(x => x.OverlapUnit == DocumentPartitionOverlapUnit.Character);
        validate.RuleFor(x => x.TokenEncodingOrModel)
            .MaximumLength(64).WithMessage("Token 编码或模型名不能超过 64 个字符.");
    }
}
