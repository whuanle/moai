#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

namespace MoAI.Infra.Jina.Models;

/// <summary>
/// Jina Search API（s.jina.ai）请求体.
/// </summary>
public class JinaSearchQuery
{
    /// <summary>
    /// 搜索词，可为关键词或自然语言问题.
    /// </summary>
    public string Q { get; set; } = string.Empty;
}
