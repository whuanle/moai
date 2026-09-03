using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MoAI.AIPlugin.Models;

namespace MoAI.AIPlugin.Services;

/// <summary>
/// 插件注册与发现服务的默认实现。首次访问时扫描当前 AppDomain 中已加载且引用了契约程序集的程序集，结果缓存.
/// </summary>
public class PluginRegistry : IPluginRegistry
{
    private const string ContractsAssemblyName = "MoAI.AIPlugin.Shared";

    private readonly ConcurrentDictionary<string, PluginInfo> _plugins = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private bool _scanned;

    /// <inheritdoc/>
    public IReadOnlyList<PluginInfo> GetAll()
    {
        EnsureScanned();
        return _plugins.Values.OrderBy(x => x.Key).ToList();
    }

    /// <inheritdoc/>
    public PluginInfo? Get(string key)
    {
        EnsureScanned();
        _plugins.TryGetValue(key, out var info);
        return info;
    }

    /// <inheritdoc/>
    public void RegisterAssembly(Assembly assembly)
    {
        ScanAssembly(assembly);
    }

    private void EnsureScanned()
    {
        if (_scanned)
        {
            return;
        }

        lock (_gate)
        {
            if (_scanned)
            {
                return;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                ScanAssembly(assembly);
            }

            _scanned = true;
        }
    }

    private void ScanAssembly(Assembly assembly)
    {
        if (assembly.IsDynamic || !ReferencesContracts(assembly))
        {
            return;
        }

        foreach (var type in GetLoadableTypes(assembly))
        {
            if (PluginTypeHelper.TryGetPluginInfo(type) is { } info)
            {
                _plugins.TryAdd(info.Key, info);
            }
        }
    }

    private static bool ReferencesContracts(Assembly assembly)
    {
        try
        {
            return assembly.GetReferencedAssemblies().Any(x =>
                string.Equals(x.Name, ContractsAssemblyName, StringComparison.Ordinal));
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t != null)!;
        }
    }
}
