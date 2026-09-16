namespace MoAI.Skill.Services;

/// <summary>
/// 技能领域服务：面向 Agent 运行时提供技能加载能力.
/// </summary>
public interface ISkillService
{
    /// <summary>
    /// 按技能 id 集合获取启用中的技能运行时信息（跳过不存在/已禁用/已删除的）.
    /// </summary>
    /// <param name="skillIds">技能 id 集合.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>技能运行时信息列表.</returns>
    Task<IReadOnlyList<SkillRuntimeInfo>> GetRuntimeSkillsAsync(IReadOnlyCollection<Guid> skillIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// 过滤出用户可见的技能 id 子集：系统内置 ∪ 公开 ∪ 所在团队 ∪ 本人个人技能，且未禁用.
    /// </summary>
    /// <param name="skillIds">待校验的技能 id 集合.</param>
    /// <param name="userId">用户 id.</param>
    /// <param name="teamId">团队 id（应用所属团队）.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>可见的技能 id 列表.</returns>
    Task<IReadOnlyList<Guid>> FilterVisibleSkillIdsAsync(IReadOnlyCollection<Guid> skillIds, long userId, int teamId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取技能包文件内容（文本）.
    /// </summary>
    /// <param name="file">技能运行时文件.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>文件文本内容.</returns>
    Task<string> ReadSkillFileAsync(SkillRuntimeFile file, CancellationToken cancellationToken = default);
}
