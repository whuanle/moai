using System;
using FluentValidation;
using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Wiki.Commands;

/// <summary>
/// AI 智能切割知识库文档（对话模型按语义输出 JSON 字符串数组），仅团队成员可操作.
/// 需先执行 <see cref="ExtractDocumentContentCommand"/> 提取内容，之后才能切割。
/// </summary>
public class AiPartitionDocumentCommand : IRequest<EmptyCommandResponse>, IModelValidator<AiPartitionDocumentCommand>
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
    /// 用于智能切割的对话模型 id.
    /// </summary>
    public Guid AiModelId { get; init; }

    /// <summary>
    /// 提示词模板，为空时使用内置默认模板（要求模型输出 JSON 字符串数组）.
    /// </summary>
    public string? PromptTemplate { get; init; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<AiPartitionDocumentCommand> validate)
    {
        validate.RuleFor(x => x.AiModelId)
            .NotEmpty().WithMessage("请选择用于智能切割的对话模型.");
    }
}
