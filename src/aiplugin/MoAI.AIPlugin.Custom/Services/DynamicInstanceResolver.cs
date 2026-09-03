using System.Linq;
using Microsoft.EntityFrameworkCore;
using MoAI.AIPlugin.Services;
using MoAI.Database;

namespace MoAI.AIPlugin.Services;

/// <summary>
/// 动态插件实例解析器的默认实现。按实例 key 查询 plugin_dynamic，取出模板 key 与配置，再用注册表解析模板元数据.
/// </summary>
public class DynamicInstanceResolver : IDynamicInstanceResolver
{
    private readonly DatabaseContext _databaseContext;
    private readonly IPluginRegistry _registry;

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicInstanceResolver"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="registry">插件注册表.</param>
    public DynamicInstanceResolver(DatabaseContext databaseContext, IPluginRegistry registry)
    {
        _databaseContext = databaseContext;
        _registry = registry;
    }

    /// <inheritdoc/>
    public DynamicInstanceResolveResult? Resolve(string instanceKey)
    {
        if (string.IsNullOrWhiteSpace(instanceKey))
        {
            return null;
        }

        var dynamicEntity = _databaseContext.PluginDynamics
            .AsNoTracking()
            .FirstOrDefault(x => x.PluginKey == instanceKey && x.IsDeleted == 0);

        if (dynamicEntity == null)
        {
            return null;
        }

        var template = _registry.Get(dynamicEntity.TempleteKey);
        if (template == null || !template.IsDynamic)
        {
            return null;
        }

        return new DynamicInstanceResolveResult
        {
            Template = template,
            ConfigJson = dynamicEntity.Config,
        };
    }
}
