using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Seed;
using MoAI.Settings.Models;
using MoAI.Settings.Services;

namespace MoAI.Settings.Services;

/// <summary>
/// 知识图谱配置读取服务.
/// </summary>
public class KnowledgeGraphSettingsService : IKnowledgeGraphSettingsService
{
    private static readonly string[] Keys =
    {
        SettingDefinitions.Neo4jEnabledKey,
        SettingDefinitions.Neo4jUriKey,
        SettingDefinitions.Neo4jUsernameKey,
        SettingDefinitions.Neo4jPasswordKey
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
    public async Task<Neo4jKnowledgeGraphSettings> GetAsync(CancellationToken cancellationToken)
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

        var enabled = string.Equals(Resolve(SettingDefinitions.Neo4jEnabledKey), "true", StringComparison.OrdinalIgnoreCase);
        if (!enabled)
        {
            return new Neo4jKnowledgeGraphSettings { Enabled = false };
        }

        return new Neo4jKnowledgeGraphSettings
        {
            Enabled = true,
            Uri = Resolve(SettingDefinitions.Neo4jUriKey),
            Username = Resolve(SettingDefinitions.Neo4jUsernameKey),
            Password = Resolve(SettingDefinitions.Neo4jPasswordKey)
        };
    }
}
