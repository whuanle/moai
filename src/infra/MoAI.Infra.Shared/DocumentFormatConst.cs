namespace MoAI.Infra;

/// <summary>
/// 知识库/知识图谱文档提取支持的规范格式清单（单一真源，前端常量与设置默认值对齐）.
/// <para>
/// 分三类提取能力：
/// 1. Maomi.ToMarkdown 原生提取（pdf/docx/xlsx/pptx/html/md/txt/json 等 10 种）；
/// 2. 平台自研提取器（epub/odt/rtf）；
/// 3. 纯文本兜底（代码/配置类，嗅探排除二进制后按文本读取）。
/// </para>
/// </summary>
public static class DocumentFormatConst
{
    /// <summary>
    /// Maomi.ToMarkdown 原生支持的扩展名（与库内 MimeTypesDetection 对齐）.
    /// </summary>
    public static readonly IReadOnlyList<string> NativeFormats = new string[]
    {
        ".pdf",
        ".docx",
        ".xlsx",
        ".pptx",
        ".html",
        ".htm",
        ".md",
        ".markdown",
        ".txt",
        ".json",
    };

    /// <summary>
    /// 知识库默认允许上传的文件类型（42 种，文档/文本提取能力全集，图片暂不支持）.
    /// <para>
    /// 即用户可用格式全集：41 种需求格式 + 保留历史默认的 .markdown；顺序即展示顺序.
    /// </para>
    /// </summary>
    public static readonly IReadOnlyList<string> ExtractableFormats = new string[]
    {
        ".bat",
        ".c",
        ".conf",
        ".cpp",
        ".css",
        ".csv",
        ".docx",
        ".epub",
        ".go",
        ".h",
        ".hpp",
        ".htm",
        ".html",
        ".ini",
        ".java",
        ".js",
        ".json",
        ".less",
        ".log",
        ".md",
        ".markdown",
        ".mdx",
        ".odt",
        ".pdf",
        ".php",
        ".pptx",
        ".properties",
        ".py",
        ".rb",
        ".rtf",
        ".scss",
        ".sh",
        ".sql",
        ".swift",
        ".tex",
        ".textpack",
        ".ts",
        ".txt",
        ".xlsx",
        ".xml",
        ".yaml",
        ".yml",
    };

    /// <summary>
    /// <see cref="ExtractableFormats"/> 拼接串（设置默认值/上传白名单报错提示用）.
    /// </summary>
    public static readonly string ExtractableFormatsJoined = string.Join(",", ExtractableFormats);
}