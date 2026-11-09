using System.Collections.Generic;
using System.Text;
using MoAI.KnowledgeGraph.Models;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 图检索片段构造器（共享）：命中实体子图的文本化格式唯一真源，
/// 供 <see cref="IGraphSearchService"/> 实现（检索 API 的 Text）与工作流 Client（kgSearch 节点的 contents/text）共用，保证两侧同源同形.
/// </summary>
public static class GraphSearchTextHelper
{
    /// <summary>
    /// 子图文本总长上限；超长时截断并追加省略标记（截断后总长可超出该值，与检索 API 既有行为一致）.
    /// </summary>
    public const int MaxTextLength = 8192;

    /// <summary>
    /// 构造命中实体的首行头部：名称（实体类型名）+ 描述；实体类型名缺失时兜底「未知类型」.
    /// 格式与 GraphSearchService 文本化的单段头部逐字节一致.
    /// </summary>
    /// <param name="hit">图检索命中.</param>
    /// <returns>头部文本（不含换行符）.</returns>
    public static string BuildHitHeader(GraphSearchHit hit)
        => $"【{hit.Name}（{hit.EntityTypeName ?? "未知类型"}）】{hit.Description}";

    /// <summary>
    /// 构造单条邻居行：两空格缩进 + 关系类型名（缺失时兜底「关联」）+ 方向 + 邻居名与描述.
    /// 格式与 GraphSearchService 文本化的邻居行逐字节一致.
    /// </summary>
    /// <param name="neighbor">邻居关系摘要.</param>
    /// <returns>邻居行文本（不含换行符）.</returns>
    public static string BuildNeighborLine(GraphNeighbor neighbor)
        => $"  └─ {neighbor.RelationName ?? "关联"}({neighbor.Direction})→ {neighbor.Name}：{neighbor.Description}";

    /// <summary>
    /// 构造单个命中的完整文本化片段：头部 + 换行 + 逐条邻居行（段内换行用 \n，段间分隔由调用方拼接）.
    /// </summary>
    /// <param name="hit">图检索命中.</param>
    /// <param name="neighbors">该命中的一跳邻居（通常传 <see cref="GraphSearchHit.Neighbors"/>）.</param>
    /// <returns>片段文本.</returns>
    public static string BuildHitFragment(GraphSearchHit hit, IReadOnlyList<GraphNeighbor> neighbors)
    {
        var builder = new StringBuilder(BuildHitHeader(hit));
        foreach (var neighbor in neighbors)
        {
            builder.Append('\n').Append(BuildNeighborLine(neighbor));
        }

        return builder.ToString();
    }

    /// <summary>
    /// 超长截断：超过 <see cref="MaxTextLength"/> 时截取前缀并追加「…(已截断)」，否则原样返回.
    /// </summary>
    /// <param name="text">原始文本.</param>
    /// <returns>截断后的文本.</returns>
    public static string Truncate(string text)
        => text.Length <= MaxTextLength ? text : text[..MaxTextLength] + "…(已截断)";

    /// <summary>
    /// 多命中整段组装（预算裁剪，B3）：按传入顺序（上游已按得分降序）逐段填充，
    /// 预算耗尽时丢弃后续整段（低分命中优先被裁）并置截断标记；单段自身超限走前缀截断兜底.
    /// </summary>
    /// <param name="hits">命中列表（得分降序）.</param>
    /// <returns>文本化结果与是否截断.</returns>
    public static (string Text, bool Truncated) BuildHitsText(IReadOnlyList<GraphSearchHit> hits)
    {
        var builder = new StringBuilder();
        var truncated = false;
        foreach (var hit in hits)
        {
            var fragment = BuildHitFragment(hit, hit.Neighbors) + "\n\n";
            if (builder.Length > 0 && builder.Length + fragment.Length > MaxTextLength)
            {
                truncated = true;
                break;
            }

            builder.Append(fragment);
        }

        var text = builder.ToString().TrimEnd();
        if (text.Length > MaxTextLength)
        {
            text = Truncate(text);
            truncated = true;
        }

        return (text, truncated);
    }
}
