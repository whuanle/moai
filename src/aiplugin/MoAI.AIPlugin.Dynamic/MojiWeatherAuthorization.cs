namespace MoAI.AIPlugin.Dynamic;

/// <summary>
/// 墨迹天气插件的公共辅助方法.
/// </summary>
internal static class MojiWeatherAuthorization
{
    /// <summary>
    /// 将实例配置中的 AppCode 规范化为 <c>Authorization</c> 头可用的值（幂等追加 <c>APPCODE </c> 前缀）.
    /// </summary>
    /// <param name="appCode">配置中的 AppCode，可以是裸值，也可以已带 <c>APPCODE </c> 前缀.</param>
    /// <returns>可直接作为 <c>Authorization</c> 头的字符串.</returns>
    public static string Build(string appCode)
    {
        var code = appCode.Trim();
        return code.StartsWith("APPCODE ", StringComparison.OrdinalIgnoreCase) ? code : $"APPCODE {code}";
    }
}
