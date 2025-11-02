using MediatR;
using MoAI.Infra.Models;

namespace MoAI.AiModel.Commands;

/// <summary>
/// 删除 ai 模型.
/// </summary>
public class DeleteAiModelCommand : IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// 模型 id.
    /// </summary>
    public int AiModelId { get; init; }
}
