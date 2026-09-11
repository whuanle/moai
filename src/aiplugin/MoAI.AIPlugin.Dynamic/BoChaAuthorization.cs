namespace MoAI.AIPlugin.Dynamic;

/// <summary>
/// 博查系列插件的公共辅助方法.
/// </summary>
internal static class BoChaAuthorization
{
    /// <summary>
    /// 将实例配置中的 API Key 规范化为 <c>Authorization</c> 头可用的值（幂等追加 <c>Bearer </c> 前缀）.
    /// </summary>
    /// <param name="apiKey">配置中的 API Key，可以是裸 Key，也可以已带 <c>Bearer </c> 前缀.</param>
    /// <returns>可直接作为 <c>Authorization</c> 头的字符串.</returns>
    public static string Build(string apiKey)
    {
        var key = apiKey.Trim();
        return key.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? key : $"Bearer {key}";
    }
}
