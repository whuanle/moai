namespace MoAI.AIPlugin.Services;

/// <summary>
/// 动态插件模板展示补充信息（图标/安装版本/本地分类）提供者。
/// 模板本身来自运行时注册表，展示信息来自已安装在线插件包与系统侧插件记录.
/// </summary>
public interface IPluginTemplateMetaProvider
{
    /// <summary>
    /// 批量查询模板展示补充信息；不存在的 key 不返回.
    /// </summary>
    /// <param name="keys">模板 key 集合.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>模板 key（不区分大小写）到补充信息的映射.</returns>
    Task<IReadOnlyDictionary<string, PluginTemplateMeta>> GetAsync(IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default);
}

/// <summary>模板展示补充信息.</summary>
public sealed class PluginTemplateMeta
{
    /// <summary>图标对象路径（MoAI OSS 对象 key）.</summary>
    public string AvatarPath { get; init; } = string.Empty;

    /// <summary>在线插件包安装版本.</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>系统侧插件记录上的本地分类名.</summary>
    public string CategoryName { get; init; } = string.Empty;
}
