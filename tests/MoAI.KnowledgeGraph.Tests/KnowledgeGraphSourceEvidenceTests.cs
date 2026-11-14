using System.Text.Json;
using MoAI.KnowledgeGraph.Services;
using Xunit;

namespace MoAI.KnowledgeGraph.Tests;

public class KnowledgeGraphSourceEvidenceTests
{
    [Fact]
    public void MergeDescription_AppendsDistinct_AndTruncates()
    {
        var merged = KnowledgeGraphSourceEvidence.MergeDescription("原有描述", "新描述");
        Assert.Equal("原有描述\n新描述", merged);

        // 完全相同不重复
        Assert.Equal("原有描述", KnowledgeGraphSourceEvidence.MergeDescription("原有描述", "原有描述"));

        // 超长截断
        var longText = new string('甲', 1500);
        var truncated = KnowledgeGraphSourceEvidence.MergeDescription(longText, new string('乙', 1500));
        Assert.Equal(KnowledgeGraphSourceEvidence.MaxDescriptionLength, truncated.Length);
    }

    [Fact]
    public void Append_DedupsByBatchAndDoc_AndCapsFifo()
    {
        var batch = Guid.CreateVersion7();
        var first = KnowledgeGraphSourceEvidence.Append(null, batch, "文档A.pdf");
        Assert.Contains("文档A.pdf", first);

        // 同批次同文档重复追加 → 不增长
        var second = KnowledgeGraphSourceEvidence.Append(first, batch, "文档A.pdf");
        Assert.Equal(first, second);

        // 同批次不同文档 → 追加
        var third = KnowledgeGraphSourceEvidence.Append(second, batch, "文档B.pdf");
        Assert.NotEqual(second, third);

        // 超上限 FIFO：丢最旧
        string? current = null;
        for (var i = 0; i < KnowledgeGraphSourceEvidence.MaxEntries + 5; i++)
        {
            current = KnowledgeGraphSourceEvidence.Append(current, Guid.CreateVersion7(), $"doc{i}.txt");
        }

        Assert.NotNull(current);
        var entries = JsonSerializer.Deserialize<List<EntryDto>>(current, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.Equal(KnowledgeGraphSourceEvidence.MaxEntries, entries!.Count);
        // 最旧的 doc0 已被挤出
        Assert.DoesNotContain("doc0.txt", current);
        Assert.Contains($"doc{KnowledgeGraphSourceEvidence.MaxEntries + 4}.txt", current);
    }

    private sealed record EntryDto(string B, string D);
}
