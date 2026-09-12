using System.Reflection;

namespace MoAI.Skill.Services;

/// <summary>
/// 系统内置技能清单：内嵌资源的分发目录约定 Resources/skills/{key}/{file}.
/// </summary>
public static class BuiltinSkills
{
    private static readonly IReadOnlyList<string> DocxWriterFiles = new string[]
    {
        "generate_docx.py",
    };

    private static readonly IReadOnlyList<string> PptWriterFiles = new string[]
    {
        "generate_pptx.py",
    };

    /// <summary>
    /// 获取内置技能的包内文件相对路径列表.
    /// </summary>
    /// <param name="skillKey">技能标识.</param>
    /// <returns>文件相对路径列表，非内置技能返回空.</returns>
    public static IReadOnlyList<string> GetFiles(string skillKey) => skillKey switch
    {
        "docx_writer" => DocxWriterFiles,
        "ppt_writer" => PptWriterFiles,
        _ => Array.Empty<string>(),
    };

    /// <summary>
    /// 构造内嵌资源名：MoAI.Skill.Resources.skills.{key}.{path 中的 / 替换为 .}.
    /// </summary>
    /// <param name="skillKey">技能标识.</param>
    /// <param name="path">包内相对路径.</param>
    /// <returns>资源名.</returns>
    public static string BuildResourceName(string skillKey, string path)
    {
        return $"MoAI.Skill.Resources.skills.{skillKey}.{path.Replace('/', '.')}";
    }

    /// <summary>
    /// 校验资源是否存在于指定程序集.
    /// </summary>
    /// <param name="assembly">技能核心程序集.</param>
    /// <param name="resourceName">资源名.</param>
    /// <returns>是否存在.</returns>
    public static bool ResourceExists(Assembly assembly, string resourceName)
    {
        return assembly.GetManifestResourceNames().Contains(resourceName);
    }
}
