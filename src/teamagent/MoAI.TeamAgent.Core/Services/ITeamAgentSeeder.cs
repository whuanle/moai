namespace MoAI.TeamAgent.Services;

/// <summary>
/// 内置智能体种子服务：团队创建时自动种入（幂等），也可对存量团队补种.
/// </summary>
public interface ITeamAgentSeeder
{
    /// <summary>
    /// 确保团队已种入全部内置智能体（按名称幂等：已存在同名的跳过）；返回本次实际种入数量.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>本次种入的智能体数量.</returns>
    Task<int> EnsureSeededAsync(long teamId, CancellationToken cancellationToken = default);
}
