using MediatR;
using MoAI.AI.Models;
using MoAI.Infra.Models;

namespace MoAI.AiModel.Commands;

/// <summary>
/// 添加 AI 模型.
/// </summary>
public class AddAiModelCommand : AiEndpoint, IRequest<SimpleInt>
{
    /// <summary>
    /// 公开给用户使用.
    /// </summary>
    public bool IsPublic { get; init; }
}
