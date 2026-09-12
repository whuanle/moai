namespace MoAI.Skill.Services;

/// <summary>
/// 技能运行时文件：自定义技能指向 MinIO 文件，系统内置技能指向程序集内嵌资源.
/// </summary>
public class SkillRuntimeFile
{
    /// <summary>
    /// 技能包内相对路径，加载时按此路径写入沙箱.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// MinIO 文件 id（file 表）；0 表示系统内置技能的内嵌资源文件.
    /// </summary>
    public long FileId { get; init; }

    /// <summary>
    /// 内嵌资源名（系统内置技能）.
    /// </summary>
    public string? ResourceName { get; init; }

    /// <summary>
    /// 是否内嵌资源文件.
    /// </summary>
    public bool IsEmbedded => FileId == 0 && !string.IsNullOrEmpty(ResourceName);
}
