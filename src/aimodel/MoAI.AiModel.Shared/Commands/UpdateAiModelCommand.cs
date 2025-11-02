using MediatR;
using MoAI.AiModel.Models;
using MoAI.Infra.Models;

namespace MoAI.AiModel.Commands;

/// <summary>
/// 修改 AI 模型.
/// </summary>
public class UpdateAiModelCommand : AiEndpoint, IRequest<EmptyCommandResponse>
{
    /// <summary>
    /// AI 模型 id.
    /// </summary>
    public int AiModelId { get; init; }

    /// <summary>
    /// 公开给用户使用.
    /// </summary>
    public bool IsPublic { get; init; }
}
