using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Seed;
using MoAI.Settings.Models;

namespace MoAI.Settings.Services;

/// <summary>
/// Agent 运行时默认参数读取服务.
/// </summary>
public class AgentSettingsService : IAgentSettingsService
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentSettingsService"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public AgentSettingsService(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<AgentCompactionDefaultsSettings> GetAgentCompactionDefaultsAsync(CancellationToken cancellationToken)
    {
        var value = await _databaseContext.Settings
            .Where(s => s.Key == SettingDefinitions.AgentCompactionDefaultsKey)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(value))
        {
            value = SettingDefinitions.Find(SettingDefinitions.AgentCompactionDefaultsKey)?.DefaultValue;
        }

        return AgentCompactionDefaultsSettings.Parse(value);
    }
}
