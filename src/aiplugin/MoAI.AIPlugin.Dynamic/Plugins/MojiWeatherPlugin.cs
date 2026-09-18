using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MoAI.AIPlugin.Attributes;
using MoAI.AIPlugin.Contracts;
using MoAI.AIPlugin.Dynamic.Models;
using MoAI.Infra.Exceptions;
using MoAI.Infra.MojiWeather;
using Refit;

namespace MoAI.AIPlugin.Dynamic.Plugins;

/// <summary>
/// 墨迹天气查询（动态插件）：使用实例配置中的阿里云云市场 AppCode 调用墨迹天气 broadcast 接口，返回实时天气与未来逐日预报.
/// </summary>
[AiPlugin(key: "moji_weather", Name = "墨迹天气查询", Description = "调用墨迹天气 API（阿里云云市场）查询城市或经纬度的实时天气与未来逐日预报，返回温度、天气现象、湿度、风向风力与逐日高低温等")]
public class MojiWeatherPlugin : IDynamicPluginRuntime<MojiWeatherRequest, MojiWeatherResponse, MojiWeatherConfig>
{
    private readonly IMojiWeatherClient _client;
    private MojiWeatherConfig _config = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="MojiWeatherPlugin"/> class.
    /// </summary>
    /// <param name="client">墨迹天气 API 客户端（由基础设施层注册，复用统一的外部请求日志与遥测）.</param>
    public MojiWeatherPlugin(IMojiWeatherClient client)
    {
        _client = client;
    }

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return """
            {
              "CityId": null,     // 墨迹城市 ID（与 Lat/Lon 二选一；城市 ID 列表在购买服务后的云市场控制台可下载）
              "Lat": "39.90598",  // 纬度（与 Lon 同时提供）
              "Lon": "116.39139"  // 经度
            }
            """;
    }

    /// <inheritdoc/>
    public static string GetConfigExampleValue()
    {
        return """
            {
              "AppCode": "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", // 阿里云云市场 AppCode：购买墨迹天气 API 服务后在云市场「已购买服务」控制台查看
              "Token": "" // 访问令牌：部分服务规格要求随请求下发（购买后控制台可查）；不要求的规格留空
            }
            """;
    }

    /// <inheritdoc/>
    public Task<string?> InitAsync(MojiWeatherConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.AppCode))
        {
            return Task.FromResult<string?>("AppCode 不能为空，请先在阿里云云市场购买墨迹天气 API 服务并获取 AppCode");
        }

        _config = config;
        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public async Task<MojiWeatherResponse> RunAsync(MojiWeatherRequest request, CancellationToken cancellationToken)
    {
        var cityId = request.CityId?.Trim();
        var lat = request.Lat?.Trim();
        var lon = request.Lon?.Trim();
        var hasCityId = !string.IsNullOrEmpty(cityId);
        var hasCoordinate = !string.IsNullOrEmpty(lat) && !string.IsNullOrEmpty(lon);
        if (!hasCityId && !hasCoordinate)
        {
            throw new BusinessException(400, "定位参数缺失：CityId 或 Lat+Lon 至少提供一组");
        }

        var form = new Dictionary<string, string>();
        if (hasCityId)
        {
            form["cityId"] = cityId!;
        }
        else
        {
            form["lat"] = lat!;
            form["lon"] = lon!;
        }

        var token = _config.Token?.Trim();
        if (!string.IsNullOrEmpty(token))
        {
            form["token"] = token;
        }

        string raw;
        try
        {
            raw = await _client.BroadcastAsync(MojiWeatherAuthorization.Build(_config.AppCode), form)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ApiException ex)
        {
            // Refit 对非 2xx 直接抛 ApiException，这里换成带响应内容的业务异常，
            // 便于在运行抽屉中定位（401 AppCode 无效、403 未购买/欠费、429 限流等）。
            throw new BusinessException((int)ex.StatusCode, $"墨迹天气调用失败（HTTP {(int)ex.StatusCode}）：{ex.Content ?? ex.ReasonPhrase}");
        }

        return Parse(raw);
    }

    /// <summary>
    /// 容错解析上游响应：信封 <c>{code,msg,data}</c> 中 code!=0 视为失败；data 内 condition/forecast/city
    /// 的字段形态随服务规格变化（字符串或数字、数组或对象包装），逐层展开、取不到有效字段的条目丢弃.
    /// </summary>
    /// <param name="raw">上游原始 JSON 文本.</param>
    /// <returns>裁剪后的响应模型.</returns>
    private static MojiWeatherResponse Parse(string raw)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(raw);
        }
        catch (JsonException ex)
        {
            throw new BusinessException(502, $"墨迹天气响应不是合法 JSON：{ex.Message}");
        }

        using (document)
        {
            var root = document.RootElement;
            EnsureSuccess(root);

            var data = ReadObject(root, "data") ?? root;
            return new MojiWeatherResponse
            {
                City = MapCity(ReadObject(data, "city")),
                Condition = MapCondition(ReadObject(data, "condition")),
                Forecast = MapForecast(data),
            };
        }
    }

    private static void EnsureSuccess(JsonElement root)
    {
        var code = ReadInt64(root, "code");
        if (code is null or 0)
        {
            return;
        }

        var msg = ReadString(root, "msg") ?? ReadString(root, "message") ?? string.Empty;
        throw new BusinessException(502, $"墨迹天气返回错误（code={code}）：{msg}");
    }

    private static MojiWeatherCity? MapCity(JsonElement? element)
    {
        if (element is null)
        {
            return null;
        }

        var city = new MojiWeatherCity
        {
            CityId = ReadString(element.Value, "cityId"),
            Name = ReadString(element.Value, "name"),
            Province = ReadString(element.Value, "pname"),
            Country = ReadString(element.Value, "counname"),
        };

        // 定位字段一个都取不到时说明该结构不认识，不产出全空对象.
        return city.CityId == null && city.Name == null && city.Province == null && city.Country == null ? null : city;
    }

    private static MojiWeatherCondition? MapCondition(JsonElement? element)
    {
        if (element is null)
        {
            return null;
        }

        var condition = new MojiWeatherCondition
        {
            Temp = ReadString(element.Value, "temp"),
            Text = ReadString(element.Value, "text") ?? ReadString(element.Value, "condition"),
            Humidity = ReadString(element.Value, "humidity"),
            WindDir = ReadString(element.Value, "windDir"),
            WindLevel = ReadString(element.Value, "windLevel"),
            WindSpeed = ReadString(element.Value, "windSpeed"),
            Pressure = ReadString(element.Value, "pressure"),
            Icon = ReadString(element.Value, "icon"),
            UpDateTime = ReadString(element.Value, "upDateTime"),
        };

        // 常用字段一个都取不到时说明该结构不认识，不产出全空对象.
        return condition.Temp == null && condition.Text == null && condition.Humidity == null
            && condition.WindDir == null && condition.Icon == null
            ? null : condition;
    }

    private static List<MojiWeatherForecastDay> MapForecast(JsonElement data)
    {
        if (data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("forecast", out var forecast))
        {
            return [];
        }

        var days = new List<MojiWeatherForecastDay>();
        foreach (var item in EnumerateItems(forecast))
        {
            var day = new MojiWeatherForecastDay
            {
                Date = ReadString(item, "date"),
                Week = ReadString(item, "week"),
                ConditionDay = ReadString(item, "conditionDay"),
                ConditionNight = ReadString(item, "conditionNight"),
                TempDay = ReadString(item, "tempDay"),
                TempNight = ReadString(item, "tempNight"),
                WindDirDay = ReadString(item, "windDirDay"),
                WindLevelDay = ReadString(item, "windLevelDay"),
                WindDirNight = ReadString(item, "windDirNight"),
                WindLevelNight = ReadString(item, "windLevelNight"),
                SunRise = ReadString(item, "sunRise"),
                SunSet = ReadString(item, "sunSet"),
            };

            // 通用字段一个都取不到时说明该条目形态不认识（例如空对象），直接丢弃.
            if (day.Date == null && day.ConditionDay == null && day.ConditionNight == null && day.TempDay == null && day.TempNight == null)
            {
                continue;
            }

            days.Add(day);
        }

        return days;
    }

    /// <summary>
    /// 展开逐日预报条目：兼容裸数组与 <c>{daily:[...]}</c> / <c>{value:[...]}</c> 包装形态.
    /// </summary>
    /// <param name="root">forecast 的 JSON 根元素.</param>
    /// <returns>逐日预报条目.</returns>
    private static IEnumerable<JsonElement> EnumerateItems(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                yield return item;
            }

            yield break;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        // 对象形态时取第一个数组属性展开（如 daily / value），无数组属性则不产出条目.
        foreach (var property in root.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in property.Value.EnumerateArray())
                {
                    yield return item;
                }

                yield break;
            }
        }
    }

    private static JsonElement? ReadObject(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return property;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => property.ToString(),
            _ => null,
        };
    }

    private static long? ReadInt64(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var value))
        {
            return value;
        }

        if (property.ValueKind == JsonValueKind.String && long.TryParse(property.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }
}
