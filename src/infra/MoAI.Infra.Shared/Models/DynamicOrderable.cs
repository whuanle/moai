namespace MoAI.Infra.Models;

/// <summary>
/// 动态排序.
/// </summary>
public class DynamicOrderable : IDynamicOrderable
{
    /// <inheritdoc/>
    public IReadOnlyCollection<KeyValueBool> OrderByFields { get; init; } = Array.Empty<KeyValueBool>();
}
