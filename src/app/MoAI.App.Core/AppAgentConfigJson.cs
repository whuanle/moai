using System.Text.Json;

namespace MoAI.App;

/// <summary>
/// <c>app_agent_config</c> 中 JSON 文本列（<c>wiki_ids</c> / <c>plugins</c>）的读写辅助.
/// </summary>
internal static class AppAgentConfigJson
{
    /// <summary>
    /// 解析 JSON 数字数组（知识库 id）；空串/非法内容返回空列表.
    /// </summary>
    /// <param name="json">JSON 文本，如 <c>[1,2]</c>.</param>
    /// <returns>知识库 id 列表.</returns>
    public static List<long> ParseLongList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<long>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<long>>(json) ?? new List<long>();
        }
        catch (JsonException)
        {
            return new List<long>();
        }
    }

    /// <summary>
    /// 解析 JSON uuid 字符串数组（插件 id）；空串/非法内容返回空列表.
    /// </summary>
    /// <param name="json">JSON 文本，如 <c>["...uuid..."]</c>.</param>
    /// <returns>插件 id 列表.</returns>
    public static List<Guid> ParseGuidList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<Guid>();
        }

        try
        {
            var raw = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            var result = new List<Guid>();
            foreach (var item in raw)
            {
                if (Guid.TryParse(item, out var id) && id != Guid.Empty)
                {
                    result.Add(id);
                }
            }

            return result;
        }
        catch (JsonException)
        {
            return new List<Guid>();
        }
    }

    /// <summary>
    /// 序列化知识库 id 列表为 JSON 数组文本.
    /// </summary>
    /// <param name="wikiIds">知识库 id 列表.</param>
    /// <returns>JSON 文本.</returns>
    public static string SerializeWikiIds(IEnumerable<long> wikiIds)
        => JsonSerializer.Serialize(wikiIds.ToList());

    /// <summary>
    /// 序列化为 JSON uuid 字符串数组文本（插件 id）.
    /// </summary>
    /// <param name="pluginIds">插件 id 列表.</param>
    /// <returns>JSON 文本.</returns>
    public static string SerializePluginIds(IEnumerable<Guid> pluginIds)
        => JsonSerializer.Serialize(pluginIds.Select(x => x.ToString()).ToList());

    /// <summary>
    /// 解析 JSON 对象文本为 <see cref="JsonElement"/>；空串/非法内容返回空对象 <c>{}</c>.
    /// </summary>
    /// <param name="json">JSON 文本.</param>
    /// <returns>JSON 对象元素.</returns>
    public static JsonElement ParseJsonObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return JsonSerializer.SerializeToElement(new Dictionary<string, object?>());
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? document.RootElement.Clone()
                : JsonSerializer.SerializeToElement(new Dictionary<string, object?>());
        }
        catch (JsonException)
        {
            return JsonSerializer.SerializeToElement(new Dictionary<string, object?>());
        }
    }
}
