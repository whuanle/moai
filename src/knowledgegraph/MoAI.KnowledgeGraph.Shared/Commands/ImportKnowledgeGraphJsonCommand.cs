using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// JSON 结构化导入（内部导入页）：直接反序列化导入页提交的 JSON 文本并落库（与外部 /import 同一管线）；
/// 仅托管图 Admin+，返回逐条导入结果.
/// </summary>
public class ImportKnowledgeGraphJsonCommand : IRequest<External.ExternalImportResponse>, IModelValidator<ImportKnowledgeGraphJsonCommand>
{
    /// <summary>
    /// 图谱 id.
    /// </summary>
    public long KnowledgeGraphId { get; init; }

    /// <summary>
    /// 导入 JSON 文本（顶层对象：mode/autoCreateTypes/validateOnly/nodes/edges）.
    /// </summary>
    public string Content { get; init; } = default!;

    /// <summary>
    /// 覆盖 JSON 内的 mode（upsert/create）；null 以 JSON 内值为准（JSON 未给默认 upsert）.
    /// </summary>
    public string? Mode { get; init; }

    /// <summary>
    /// 覆盖 JSON 内的 autoCreateTypes；null 以 JSON 内值为准（默认 false）.
    /// </summary>
    public bool? AutoCreateTypes { get; init; }

    /// <summary>
    /// 覆盖 JSON 内的 validateOnly；null 以 JSON 内值为准（默认 false）.
    /// </summary>
    public bool? ValidateOnly { get; init; }

    /// <summary>
    /// 覆盖 JSON 内的 detectDuplicates；null 以 JSON 内值为准（默认 true）.
    /// </summary>
    public bool? DetectDuplicates { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<ImportKnowledgeGraphJsonCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处不校验。
        validate.RuleFor(x => x.Content).NotEmpty().WithMessage("导入内容不能为空.").MaximumLength(4_000_000).WithMessage("导入内容最长 400 万字符.");
        validate.RuleFor(x => x.Mode).Must(x => x == null || x == External.ImportExternalGraphDataCommand.ModeUpsert || x == External.ImportExternalGraphDataCommand.ModeCreate).WithMessage("mode 仅支持 upsert 或 create.");
    }
}
