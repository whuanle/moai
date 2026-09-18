using System.Collections.Generic;
using System.Threading.Tasks;
using Refit;

namespace MoAI.Infra.MojiWeather;

/// <summary>
/// 墨迹天气 API 客户端接口（阿里云云市场网关，APPCODE 鉴权）.
/// </summary>
public interface IMojiWeatherClient
{
    /// <summary>
    /// 实况天气与未来预报（POST /whapi/json/aliweather/broadcast，表单编码：cityId 或 lat+lon，可选 token）.
    /// </summary>
    /// <param name="authorization">Authorization 头，形如「APPCODE {AppCode}」.</param>
    /// <param name="form">表单参数.</param>
    /// <returns>上游原始 JSON 文本（字段形态随服务规格变化，由插件层容错解析）.</returns>
    [Post("/whapi/json/aliweather/broadcast")]
    Task<string> BroadcastAsync([Header("Authorization")] string authorization, [Body(BodySerializationMethod.UrlEncoded)] Dictionary<string, string> form);
}
