using System;
using System.Collections.Generic;
using System.Text.Json;
using MoAI.TeamAgent.Models;

namespace MoAI.TeamAgent;

/// <summary>
/// <c>team_agent</c> 中 JSON 文本列（<c>main_plugin_ids</c> / <c>main_wiki_ids</c> / <c>subs</c>）的读写辅助.
/// </summary>
internal static class TeamAgentJson
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
            var raw = JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? new List<string>();
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
            return JsonSerializer.Deserialize<List<long>>(json, JsonOptions) ?? new List<long>();
        }
        catch (JsonException)
        {
            return new List<long>();
        }
    }

    /// <summary>
    /// 解析子智能体列表 JSON；空串/非法内容返回空列表.
    /// </summary>
    /// <param name="json">JSON 文本.</param>
    /// <returns>子智能体列表.</returns>
    public static List<TeamAgentSubItem> ParseSubs(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<TeamAgentSubItem>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<TeamAgentSubItem>>(json, JsonOptions) ?? new List<TeamAgentSubItem>();
        }
        catch (JsonException)
        {
            return new List<TeamAgentSubItem>();
        }
    }

    /// <summary>
    /// 序列化插件 id 列表为 JSON uuid 字符串数组文本.
    /// </summary>
    /// <param name="pluginIds">插件 id 列表.</param>
    /// <returns>JSON 文本.</returns>
    public static string SerializePluginIds(IEnumerable<Guid> pluginIds)
        => JsonSerializer.Serialize(pluginIds.Select(x => x.ToString()).ToList(), JsonOptions);

    /// <summary>
    /// 序列化知识库 id 列表为 JSON 数字数组文本.
    /// </summary>
    /// <param name="wikiIds">知识库 id 列表.</param>
    /// <returns>JSON 文本.</returns>
    public static string SerializeWikiIds(IEnumerable<long> wikiIds)
        => JsonSerializer.Serialize(wikiIds.ToList(), JsonOptions);

    /// <summary>
    /// 序列化子智能体列表为 JSON 数组文本.
    /// </summary>
    /// <param name="subs">子智能体列表.</param>
    /// <returns>JSON 文本.</returns>
    public static string SerializeSubs(IReadOnlyList<TeamAgentSubItem> subs)
        => JsonSerializer.Serialize(subs, JsonOptions);
}
