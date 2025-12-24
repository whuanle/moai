#pragma warning disable CA1031 // 不捕获常规异常类型

using Maomi;
using Microsoft.EntityFrameworkCore;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.MemoryStorage;
using Microsoft.SemanticKernel;
using MoAI.Database;
using System.ComponentModel;
using System.Text;

namespace MoAI.Wiki.WikiPlugin;

/// <summary>
/// 知识库插件.
/// </summary>
internal class WikiPlugin
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMemoryDb _memoryDb;
    private readonly int _wikiId;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiPlugin"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="memoryDb"></param>
    /// <param name="wikiId"></param>
    public WikiPlugin(DatabaseContext databaseContext, IMemoryDb memoryDb, int wikiId)
    {
        _databaseContext = databaseContext;
        _memoryDb = memoryDb;
        _wikiId = wikiId;
    }

    /// <summary>
    /// 从知识库中查找信息.
    /// </summary>
    /// <param name="question">用户提问</param>
    /// <returns>知识库搜索结果</returns>
    [KernelFunction("invoke")]
    [Description("从知识库搜相关资料")]
    public async Task<string> InvokeAsync([Description("优化为适合在知识库中搜索的简洁关键词，去除多余的描述和礼貌用语，确保关键词准确反映用户的查询意图")] string question)
    {
        var wikiIndex = _wikiId.ToString();

        MemoryFilter filter = new MemoryFilter();

        var results = await _memoryDb.GetSimilarListAsync(index: wikiIndex, question, new[] { filter }, 0, 20, true).ToArrayAsync();

        if (results.Length == 0)
        {
            return string.Empty;
        }

        // 获取关联的文本块，包括元数据的
        var embeddingIds = results.Select(x => x.Item1.Id)
            .Distinct()
            .Select(x => Guid.Parse(x!)).ToArray();

        // 工具 chunkId 获取各个文本块
        var metadataEmbeddingIds = await _databaseContext.WikiDocumentChunkEmbeddings.AsNoTracking()
            .Where(x => embeddingIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.ChunkId);

        HashSet<Guid> chunkIds = new();
        foreach (var item in metadataEmbeddingIds)
        {
            if (item.Value == default)
            {
                chunkIds.Add(item.Key);
            }
            else if (item.Value != default)
            {
                chunkIds.Add(item.Value);
            }
        }

        // todo: 内容重排
        var chunkEmbeddings = await _databaseContext.WikiDocumentChunkEmbeddings.AsNoTracking()
            .Where(x => chunkIds.Contains(x.Id))
            .OrderBy(x => x.Id)
            .Select(x => x.DerivativeContent)
            .ToListAsync();

        if (chunkEmbeddings.Count == 0)
        {
            return string.Empty;
        }

        StringBuilder stringBuilder = new();

        foreach (var item in chunkEmbeddings)
        {
            stringBuilder.AppendLine(item);
        }

        return stringBuilder.ToString();
    }
}