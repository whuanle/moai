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
    /// 读取技能包文件内容（文本）.
    /// </summary>
    /// <param name="file">技能运行时文件.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>文件文本内容.</returns>
    Task<string> ReadSkillFileAsync(SkillRuntimeFile file, CancellationToken cancellationToken = default);
}
