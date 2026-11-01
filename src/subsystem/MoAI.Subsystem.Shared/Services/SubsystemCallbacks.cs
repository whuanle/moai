using System.Text.Json;

namespace MoAI.Subsystem.Services;

/// <summary>
/// 委托 SSO 回调地址列表（JSON 字符串数组）与实体列文本互转.
/// </summary>
public static class SubsystemCallbacks
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// 解析实体列文本为地址列表（空/损坏文本回退空列表）.
    /// </summary>
    /// <param name="json">实体列文本.</param>
    /// <returns>返回地址列表（保序）.</returns>
    public static List<string> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, Options) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>
    /// 序列化地址列表为实体列文本（去重保序）.
    /// </summary>
    /// <param name="urls">地址列表.</param>
    /// <returns>返回 JSON 文本.</returns>
    public static string Serialize(IEnumerable<string> urls)
    {
        var distinct = urls
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return JsonSerializer.Serialize(distinct, Options);
    }
}
