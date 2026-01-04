namespace MoAI.Infra.Models;

/// <summary>
/// 动态排序接口.
/// </summary>
public interface IDynamicOrderable
{
    /// <summary>
    /// 排序字段,默认是 OrderBy 升序排列，true 则是降序..
    /// </summary>
    public IReadOnlyCollection<KeyValueBool> OrderByFields { get; init; }
}
