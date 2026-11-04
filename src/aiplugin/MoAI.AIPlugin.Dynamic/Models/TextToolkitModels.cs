using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// 文本工具集配置.
/// </summary>
public class TextToolkitConfig
{
    /// <summary>
    /// 单次处理的最大文本长度（字符数），超出拒绝执行，防止超大输入占用内存.
    /// </summary>
    [Description("单次处理的最大文本长度（字符数），100-1000000，超出拒绝执行")]
    public int MaxTextLength { get; set; } = 10000;
}

/// <summary>
/// slugify 工具请求：把文本转换为 URL 友好的 slug.
/// </summary>
public class TextToolkitSlugifyRequest
{
    /// <summary>
    /// 获取参数运行示例 JSON.
    /// </summary>
    /// <returns>示例 JSON 字符串.</returns>
    public static string GetParamsExampleValue()
    {
        return """
            {
            "Text":"Hello World 2!", // 原始文本
            "Separator":"-" // 单词分隔符，可选，默认 -
            }
            """;
    }

    /// <summary>
    /// 原始文本.
    /// </summary>
    [Description("原始文本")]
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// 单词分隔符，默认 -.
    /// </summary>
    [Description("单词分隔符，默认 -，仅允许单个字符")]
    public string? Separator { get; set; }
}

/// <summary>
/// slugify 工具响应.
/// </summary>
public class TextToolkitSlugifyResponse
{
    /// <summary>
    /// 转换后的 slug.
    /// </summary>
    [Description("转换后的 slug")]
    public string Slug { get; set; } = string.Empty;
}

/// <summary>
/// word_count 工具请求：统计文本的字符数/词数/行数.
/// </summary>
public class TextToolkitWordCountRequest
{
    /// <summary>
    /// 获取参数运行示例 JSON.
    /// </summary>
    /// <returns>示例 JSON 字符串.</returns>
    public static string GetParamsExampleValue()
    {
        return """
            {
            "Text":"first line\nsecond line" // 原始文本
            }
            """;
    }

    /// <summary>
    /// 原始文本.
    /// </summary>
    [Description("原始文本")]
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// word_count 工具响应.
/// </summary>
public class TextToolkitWordCountResponse
{
    /// <summary>
    /// 字符数（不含换行）.
    /// </summary>
    [Description("字符数（不含换行）")]
    public int Chars { get; set; }

    /// <summary>
    /// 词数（按空白切分）.
    /// </summary>
    [Description("词数（按空白切分）")]
    public int Words { get; set; }

    /// <summary>
    /// 行数.
    /// </summary>
    [Description("行数")]
    public int Lines { get; set; }
}

/// <summary>
/// reverse 工具请求：反转文本.
/// </summary>
public class TextToolkitReverseRequest
{
    /// <summary>
    /// 获取参数运行示例 JSON.
    /// </summary>
    /// <returns>示例 JSON 字符串.</returns>
    public static string GetParamsExampleValue()
    {
        return """
            {
            "Text":"abc" // 原始文本
            }
            """;
    }

    /// <summary>
    /// 原始文本.
    /// </summary>
    [Description("原始文本")]
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// reverse 工具响应.
/// </summary>
public class TextToolkitReverseResponse
{
    /// <summary>
    /// 反转后的文本.
    /// </summary>
    [Description("反转后的文本")]
    public string Reversed { get; set; } = string.Empty;
}
