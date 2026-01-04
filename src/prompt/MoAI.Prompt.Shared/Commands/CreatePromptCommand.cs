using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Prompt.Commands;

/// <summary>
/// 创建提示词.
/// </summary>
public class CreatePromptCommand : IRequest<SimpleInt>
{
    /// <summary>
    /// 分类.
    /// </summary>
    public int PromptClassId { get; init; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = default!;

    /// <summary>
    /// 助手设定,支持 markdown.
    /// </summary>
    public string Content { get; init; } = default!;
}
