namespace MoAI.Infra.Models;

/// <summary>
/// 简单类型.
/// </summary>
/// <typeparam name="T">任何类型.</typeparam>
public class Simple<T>
{
    /// <summary>
    /// 任何类型.
    /// </summary>
    public T Value { get; init; } = default!;
}
