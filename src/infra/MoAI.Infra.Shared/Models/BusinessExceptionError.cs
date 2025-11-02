namespace MoAI.Infra.Models;

/// <summary>
/// 错误信息.
/// </summary>
public class BusinessExceptionError
{
    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 错误信息列表.
    /// </summary>
    public IReadOnlyCollection<string> Errors { get; init; } = default!;
}