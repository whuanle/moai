namespace MoAI.Skill.Queries.Responses;

/// <summary>
/// 技能包文件下载地址项.
/// </summary>
public class SkillFileDownloadItem
{
    /// <summary>
    /// 技能包内相对路径.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// 原始文件名.
    /// </summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>
    /// 预签名下载地址（1 小时有效）.
    /// </summary>
    public Uri DownloadUrl { get; init; } = default!;
}

/// <summary>
/// 技能包文件下载地址响应.
/// </summary>
public class QuerySkillFileDownloadResponse
{
    /// <summary>
    /// 技能标识.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// 文件下载地址列表.
    /// </summary>
    public IReadOnlyList<SkillFileDownloadItem> Items { get; init; } = Array.Empty<SkillFileDownloadItem>();
}
