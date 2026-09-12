using System.Text.Json.Serialization;

namespace MoAI.Skill.Models;

/// <summary>
/// 技能包文件项，序列化为 skill.files JSON 列.
/// </summary>
public class SkillFileItem
{
    /// <summary>
    /// 技能包内相对路径，如 <c>scripts/generate_docx.py</c>，加载时按此路径写入沙箱.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// MinIO 文件 id（file 表）.
    /// </summary>
    public long FileId { get; set; }

    /// <summary>
    /// 原始文件名.
    /// </summary>
    public string FileName { get; set; } = string.Empty;
}
