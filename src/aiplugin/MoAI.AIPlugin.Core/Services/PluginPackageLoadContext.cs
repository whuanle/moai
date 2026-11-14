using System.Reflection;
using System.Runtime.Loader;

namespace MoAI.AIPlugin.Services;

/// <summary>
/// 插件包的依赖加载上下文。每个包使用独立私有依赖目录，但 MoAI 插件契约始终复用默认上下文中的类型身份.
/// </summary>
public sealed class PluginPackageLoadContext : AssemblyLoadContext
{
    private static readonly string[] SharedPrefixes = ["MoAI.", "Maomi.", "MediatR", "Microsoft.Extensions."];
    private readonly AssemblyDependencyResolver _resolver;

    /// <summary>
    /// 创建插件包加载上下文.
    /// </summary>
    /// <param name="entryAssemblyPath">插件入口程序集绝对路径.</param>
    public PluginPackageLoadContext(string entryAssemblyPath)
        : base($"MoAIPlugin:{Path.GetFileNameWithoutExtension(entryAssemblyPath)}:{Guid.CreateVersion7():N}", isCollectible: false)
    {
        _resolver = new AssemblyDependencyResolver(entryAssemblyPath);
    }

    /// <inheritdoc/>
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var name = assemblyName.Name ?? string.Empty;
        if (SharedPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return AssemblyLoadContext.Default.Assemblies.FirstOrDefault(x =>
                    string.Equals(x.GetName().Name, name, StringComparison.OrdinalIgnoreCase))
                ?? AssemblyLoadContext.Default.LoadFromAssemblyName(assemblyName);
        }

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path == null ? null : LoadFromAssemblyPath(path);
    }

    /// <inheritdoc/>
    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path == null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
    }
}
