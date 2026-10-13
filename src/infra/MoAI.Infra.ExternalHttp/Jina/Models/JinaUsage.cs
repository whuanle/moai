#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

namespace MoAI.Infra.Jina.Models;

/// <summary>
/// Jina AI 接口 token 用量.
/// </summary>
public class JinaUsage
{
    /// <summary>
    /// 本次调用消耗的 token 数.
    /// </summary>
    public int? Tokens { get; init; }
}
