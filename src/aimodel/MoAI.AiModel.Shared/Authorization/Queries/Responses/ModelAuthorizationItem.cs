using MoAI.AI.Models;

namespace MoAI.AiModel.Authorization.Queries.Responses;

/// <summary>
/// 模型授权信息项.
/// </summary>
public class ModelAuthorizationItem
{
    /// <summary>
    /// 模型ID.
    /// </summary>
    public int ModelId { get; init; }

    /// <summary>
    /// 模型名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 显示名称.
    /// </summary>
    public string Title { get; init; } = default!;

    /// <summary>
    /// 模型功能类型.
    /// </summary>
    public AiModelType AiModelType { get; set; } = default!;

    /// <summary>
    /// 模型供应商.
    /// </summary>
    public AiProvider AiProvider { get; set; } = default!;

    /// <summary>
    /// 授权的团队列表.
    /// </summary>
    public IReadOnlyCollection<AuthorizedTeamItem> AuthorizedTeams { get; init; } = new List<AuthorizedTeamItem>();
}
