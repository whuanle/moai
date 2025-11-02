using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Prompt.Commands;

/// <summary>
/// 删除提示词分类.
/// </summary>
public class DeletePromptClassifyCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 分类 id.
    /// </summary>
    public int ClassifyId { get; init; }
}
