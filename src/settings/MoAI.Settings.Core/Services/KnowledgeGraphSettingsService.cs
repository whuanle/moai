using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Seed;
using MoAI.Settings.Models;
using MoAI.Settings.Services;

namespace MoAI.Settings.Services;

/// <summary>
/// 知识图谱图数据库配置读取服务.
/// </summary>
public class KnowledgeGraphSettingsService : IKnowledgeGraphSettingsService
{
    private static readonly string[] Keys =
    {
        SettingDefinitions.GraphEnabledKey,
        SettingDefinitions.GraphUriKey,
        SettingDefinitions.GraphUsernameKey,
        SettingDefinitions.GraphPasswordKey,
        SettingDefinitions.GraphDialectKey
    };

    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="KnowledgeGraphSettingsService"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public KnowledgeGraphSettingsService(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<KnowledgeGraphStoreSettings> GetAsync(CancellationToken cancellationToken)
    {
        var values = await _databaseContext.Settings
            .Where(s => Keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

        string Resolve(string key)
        {
            if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            return SettingDefinitions.Find(key)?.DefaultValue ?? string.Empty;
        }

        var enabled = string.Equals(Resolve(SettingDefinitions.GraphEnabledKey), "true", StringComparison.OrdinalIgnoreCase);
        if (!enabled)
        {
            return new KnowledgeGraphStoreSettings { Enabled = false };
        }

        var dialect = Resolve(SettingDefinitions.GraphDialectKey).Trim().ToLowerInvariant();
        if (dialect != KnowledgeGraphStoreSettings.DialectMemgraph && dialect != KnowledgeGraphStoreSettings.DialectNeo4j)
        {
            dialect = KnowledgeGraphStoreSettings.DialectMemgraph;
        }

        return new KnowledgeGraphStoreSettings
        {
            Enabled = true,
            Uri = Resolve(SettingDefinitions.GraphUriKey),
            Username = Resolve(SettingDefinitions.GraphUsernameKey),
            Password = Resolve(SettingDefinitions.GraphPasswordKey),
            Dialect = dialect
        };
    }
}
