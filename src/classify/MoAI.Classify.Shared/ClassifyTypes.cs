namespace MoAI.Classify;

/// <summary>
/// 分类类型常量（固定字符串，全站唯一事实来源）.
/// <para>其它模块引用分类类型时应使用本类，避免各自手写字符串.</para>
/// </summary>
public static class ClassifyTypes
{
    /// <summary>
    /// 插件类型.
    /// </summary>
    public const string Plugin = "plugin";

    /// <summary>
    /// 应用类型.
    /// </summary>
    public const string App = "app";

    /// <summary>
    /// 知识库类型.
    /// </summary>
    public const string Kb = "kb";

    /// <summary>
    /// 所有合法分类类型.
    /// </summary>
    public static readonly string[] All = [Plugin, App, Kb];
}
