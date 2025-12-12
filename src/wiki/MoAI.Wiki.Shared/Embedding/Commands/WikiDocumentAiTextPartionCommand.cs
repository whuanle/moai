using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.DocumentEmbedding.Commands;

/// <summary>
/// 使用 AI 智能切割
/// </summary>
public class WikiDocumentAiTextPartionCommand : IRequest<EmptyCommandResponse>, IModelValidator<WikiDocumentAiTextPartionCommand>
{
    /// <summary>
    /// 知识库id.
    /// </summary>
    public int WikiId { get; init; }

    /// <summary>
    /// 知识库 id.
    /// </summary>
    public int DocumentId { get; init; }

    /// <summary>
    /// Ai 模型.
    /// </summary>
    public int AiModelId { get; init; }

    /// <summary>
    /// 提示词模板.
    /// </summary>
    public string PromptTemplate { get; init; } = string.Empty;

    /// <inheritdoc/>
    public void Validate(AbstractValidator<WikiDocumentAiTextPartionCommand> validate)
    {
        validate.RuleFor(x => x.WikiId).GreaterThan(0).WithMessage("知识库 ID 必须大于 0");
        validate.RuleFor(x => x.DocumentId).GreaterThan(0).WithMessage("文档 ID 必须大于 0");
        validate.RuleFor(x => x.AiModelId).GreaterThan(0).WithMessage("AI 模型 ID 必须大于 0");
        validate.RuleFor(x => x.PromptTemplate).NotEmpty().WithMessage("提示词模板不能为空");
    }
}