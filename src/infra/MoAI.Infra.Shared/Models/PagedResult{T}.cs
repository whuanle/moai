namespace MoAI.Infra.Models;

/// <summary>
/// 分页结果.
/// </summary>
/// <typeparam name="T">结果元素类型.</typeparam>
public class PagedResult<T> : PagedParamter
{
    /// <summary>
    /// 项目集合.
    /// </summary>
    public required IReadOnlyCollection<T> Items { get; init; }

    /// <summary>
    /// 总数.
    /// </summary>
    public int Total { get; init; }
}