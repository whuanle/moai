using FluentValidation;
using MediatR;
using MoAI.Infra.Models;
using MoAI.KnowledgeGraph.Queries.Responses;

namespace MoAI.KnowledgeGraph.Commands;

/// <summary>
/// AI 导入文件生成图谱：读取已直传的文档文件，Maomi.ToMarkdown 提取内容后由对话模型按图谱现有模型抽取实体与关系写入图库，仅 Owner/Admin 的托管图可导入.
/// </summary>
public class ImportKnowledgeGraphFromFileCommand : IRequest<ImportKnowledgeGraphFromFileResponse>, IModelValidator<ImportKnowledgeGraphFromFileCommand>
{
    /// <summary>
    /// 图谱 id，由 Controller 从路由参数回填.
    /// </summary>
    public long KnowledgeGraphId { get; init; }

    /// <summary>
    /// 文件 ObjectKey（存储直传完成，须位于公开 chat 目录）.
    /// </summary>
    public string ObjectKey { get; init; } = default!;

    /// <summary>
    /// 原始文件名（用于内容提取的格式识别）.
    /// </summary>
    public string FileName { get; init; } = default!;

    /// <summary>
    /// 团队可用的 AI 对话模型 id.
    /// </summary>
    public Guid AiModelId { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<ImportKnowledgeGraphFromFileCommand> validate)
    {
        // KnowledgeGraphId 由 Controller 从路由参数回填，自动验证发生在回填之前，因此此处只校验请求体字段.
        validate.RuleFor(x => x.ObjectKey).NotEmpty().WithMessage("文件不能为空.")
            .Must(x => x.StartsWith("public/chat/", StringComparison.Ordinal))
            .WithMessage("文件 ObjectKey 不合法.");
        validate.RuleFor(x => x.FileName).NotEmpty().WithMessage("文件名不能为空.").MaximumLength(255).WithMessage("文件名最长 255 个字符.");
        validate.RuleFor(x => x.AiModelId).NotEmpty().WithMessage("请选择 AI 对话模型.");
    }
}
