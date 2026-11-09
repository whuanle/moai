using System.Text.Encodings.Web;
using System.Text.Json;

namespace MoAI.KnowledgeGraph.Services;

/// <summary>
/// 节点来源证据（srcJson）：导入批次与文档名数组，AI 导入幂等合并的记账载体（D26）.
/// </summary>
public static class KnowledgeGraphSourceEvidence
{
    /// <summary>
    /// 描述合并后的截断上限（与实体合并口径一致）.
    /// </summary>
    public const int MaxDescriptionLength = 2000;

    /// <summary>
    /// 单节点证据条目上限（FIFO，超出丢最旧）.
    /// </summary>
    public const int MaxEntries = 20;

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // 中文文档名保持可读（默认编码器会转义为 \uXXXX）
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private sealed record Entry(string B, string D);

    /// <summary>
    /// 描述合并：现有描述在前、新描述去重追加（完全相同不重复），超长截断.
    /// </summary>
    public static string MergeDescription(string existing, string? incoming)
    {
        if (string.IsNullOrWhiteSpace(incoming) || string.Equals(existing.Trim(), incoming.Trim(), StringComparison.Ordinal))
        {
            return existing;
        }

        var merged = string.Join("\n", new[] { existing, incoming }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal));
        return merged.Length > MaxDescriptionLength ? merged[..MaxDescriptionLength] : merged;
    }

    /// <summary>
    /// 追加一条来源证据（按批次 id + 文档名去重），FIFO 封顶；空列表返回 null.
    /// </summary>
    public static string? Append(string? srcJson, Guid batchId, string docName)
    {
        List<Entry> entries = [];
        if (!string.IsNullOrWhiteSpace(srcJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<List<Entry>>(srcJson, Options);
                if (parsed != null) entries.AddRange(parsed);
            }
            catch (JsonException)
            {
                // 旧数据损坏时按空处理，重新开始记账
            }
        }

        var batch = batchId.ToString();
        entries.RemoveAll(x => x.B == batch && string.Equals(x.D, docName, StringComparison.Ordinal));
        entries.Add(new Entry(batch, docName));
        if (entries.Count > MaxEntries)
        {
            entries = entries.Skip(entries.Count - MaxEntries).ToList();
        }

        return JsonSerializer.Serialize(entries, Options);
    }
}
