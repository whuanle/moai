using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MoAI.AIPlugin.Services;
using MoAI.Database;

namespace MoAI.AIPlugin.Custom.Services;

/// <summary>
/// 动态插件模板展示补充信息提供者：模板图标/安装版本来自在线插件包安装快照，
/// 本地分类名来自系统侧插件记录（team_id=0）对应的插件分类.
/// </summary>
public sealed class PluginTemplateMetaProvider : IPluginTemplateMetaProvider
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginTemplateMetaProvider"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public PluginTemplateMetaProvider(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, PluginTemplateMeta>> GetAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, PluginTemplateMeta>(StringComparer.OrdinalIgnoreCase);
        if (keys == null || keys.Count == 0)
        {
            return result;
        }

        var keyList = keys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (keyList.Count == 0)
        {
            return result;
        }

        // 模板与在线插件包同 key：图标（MoAI OSS 对象 key）与安装版本取自安装快照。
        var packages = await _databaseContext.PluginPackages
            .Where(x => keyList.Contains(x.PluginKey))
            .Select(x => new { x.PluginKey, x.AvatarPath, x.Version })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var packageByKey = packages
            .GroupBy(x => x.PluginKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // 分类名取自系统侧插件记录（安装时按包内文字分类映射到本地分类）。
        var pluginRows = await _databaseContext.Plugins
            .Where(x => x.TeamId == 0 && keyList.Contains(x.PluginName))
            .Select(x => new { x.PluginName, x.ClassifyId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var classifyIds = pluginRows
            .Select(x => x.ClassifyId)
            .Where(id => id != 0)
            .Distinct()
            .ToList();
        var classifyNames = classifyIds.Count == 0
            ? new Dictionary<int, string>()
            : await _databaseContext.Classifies
                .Where(x => classifyIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken)
                .ConfigureAwait(false);

        foreach (var key in keyList)
        {
            packageByKey.TryGetValue(key, out var package);
            var plugin = pluginRows.FirstOrDefault(x => string.Equals(x.PluginName, key, StringComparison.OrdinalIgnoreCase));
            var categoryName = plugin != null
                && plugin.ClassifyId != 0
                && classifyNames.TryGetValue(plugin.ClassifyId, out var name)
                ? name
                : string.Empty;

            if (package == null && string.IsNullOrEmpty(categoryName))
            {
                continue;
            }

            result[key] = new PluginTemplateMeta
            {
                AvatarPath = package?.AvatarPath ?? string.Empty,
                Version = package?.Version ?? string.Empty,
                CategoryName = categoryName,
            };
        }

        return result;
    }
}
