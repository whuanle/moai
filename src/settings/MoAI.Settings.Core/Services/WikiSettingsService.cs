using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Seed;
using MoAI.Settings.Services;

namespace MoAI.Settings.Services;

/// <summary>
/// 知识库设置读取服务.
/// </summary>
public class WikiSettingsService : IWikiSettingsService
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiSettingsService"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public WikiSettingsService(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<int> GetMaxFileSizeMbAsync(CancellationToken cancellationToken)
    {
        var key = SettingDefinitions.WikiMaxFileSizeKey;
        var value = await _databaseContext.Settings
            .Where(s => s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(value))
        {
            value = SettingDefinitions.Find(key)?.DefaultValue;
        }

        if (!int.TryParse(value, out var maxFileSizeMb) || maxFileSizeMb < 0)
        {
            return 0;
        }

        return maxFileSizeMb;
    }
}
