#pragma warning disable CA1822 // 将成员标记为 static
#pragma warning disable CA1031 // 不捕获常规异常类型
#pragma warning disable CS1591 // 缺少对公共可见类型或成员的 XML 注释
#pragma warning disable SA1600 // Elements should be documented

namespace MoAI.Plugin.Plugins.Doc2xPdf;

/// <summary>
/// 插件配置.
/// </summary>
public class Doc2xPdfPluginParam
{
    /// <summary>
    /// pdf 文件地址.
    /// </summary>
    public string Url { get; init; } = string.Empty;
}
