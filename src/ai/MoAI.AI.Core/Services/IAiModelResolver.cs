using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Entities;

namespace MoAI.AI.Services;

/// <summary>
/// 模型解析：按模型 id 解析出该团队可用的（模型, 渠道）.
/// </summary>
public interface IAiModelResolver
{
    /// <summary>
    /// 解析团队可用的模型（模型与渠道均启用，且为公开模型或已授权该团队）.
    /// </summary>
    /// <param name="modelId">模型 id（ai_model.id）.</param>
    /// <param name="teamId">团队 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>模型与渠道；不可用返回 null.</returns>
    Task<(AiModelEntity Model, AiChannelEntity Channel)?> ResolveByIdAsync(Guid modelId, int teamId, CancellationToken cancellationToken = default);
}
