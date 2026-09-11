namespace MoAI.AIPlugin.Dynamic;

/// <summary>
/// PaddleOCR 系列插件的公共辅助方法.
/// </summary>
internal static class PaddleOcrAuthorization
{
    /// <summary>
    /// 将实例配置中的 Token 规范化为 PaddleOCR 协议要求的 <c>Authorization</c> 头值.
    /// </summary>
    /// <param name="token">配置中的 Token；可以为空（部署未开启鉴权）.</param>
    /// <returns>
    /// 非空 Token 拼接为 <c>token {value}</c>；空 Token 直接返回 <see cref="string.Empty"/>，避免发送多余的 <c>token </c> 头.
    /// </returns>
    /// <remarks>
    /// PaddleOCR（AIStudio 部署）使用自定义的 <c>token</c> 鉴权方案，与 OAuth 的 <c>Bearer</c> 不同；
    /// 为避免给未开启鉴权的部署发送无意义的 <c>token </c>（带尾随空格），对空 Token 短路返回空字符串.
    /// </remarks>
    public static string Build(string? token)
    {
        var value = (token ?? string.Empty).Trim();
        return value.Length == 0 ? string.Empty : $"token {value}";
    }
}