using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AI.Models;
using MoAI.App.Queries;
using MoAI.App.Queries.Responses;
using MoAI.Database;
using MoAI.Database.Seed;

namespace MoAI.App.Handlers;

/// <summary>
/// <inheritdoc cref="QueryAgentMemoryDefaultsCommand"/>
/// </summary>
public class QueryAgentMemoryDefaultsCommandHandler : IRequestHandler<QueryAgentMemoryDefaultsCommand, QueryAgentMemoryDefaultsResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryAgentMemoryDefaultsCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public QueryAgentMemoryDefaultsCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<QueryAgentMemoryDefaultsResponse> Handle(QueryAgentMemoryDefaultsCommand request, CancellationToken cancellationToken)
    {
        // 平台记忆设置回显：向量化模型/维度统一在系统设置（MEMORY_*），应用面板只读展示
        var keys = new[]
        {
            SettingDefinitions.MemoryEnabledKey,
            SettingDefinitions.MemoryEmbeddingModelKey,
            SettingDefinitions.MemoryEmbeddingDimensionsKey,
        };
        var values = await _databaseContext.Settings.AsNoTracking()
            .Where(s => keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);
        string Resolve(string key) => values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : SettingDefinitions.Find(key)?.DefaultValue ?? string.Empty;

        var platformEnabled = string.Equals(Resolve(SettingDefinitions.MemoryEnabledKey).Trim(), "true", StringComparison.OrdinalIgnoreCase);
        Guid.TryParse(Resolve(SettingDefinitions.MemoryEmbeddingModelKey).Trim(), out var modelId);
        var dimensions = int.TryParse(Resolve(SettingDefinitions.MemoryEmbeddingDimensionsKey).Trim(), out var parsed)
            ? Math.Clamp(parsed, 1, 2000)
            : AgentMemorySettings.DefaultEmbeddingDimensions;

        var modelName = string.Empty;
        if (modelId != Guid.Empty)
        {
            modelName = await _databaseContext.AiModels.AsNoTracking()
                .Where(x => x.Id == modelId)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
        }

        return new QueryAgentMemoryDefaultsResponse
        {
            Prompt = AgentMemorySettings.DefaultPrompt,
            PlatformEnabled = platformEnabled,
            EmbeddingModelName = modelName,
            EmbeddingDimensions = dimensions,
        };
    }
}
