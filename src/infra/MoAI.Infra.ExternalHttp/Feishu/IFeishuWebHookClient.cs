
#pragma warning disable CS1591 // 缺少对公共可见类型或成员的 XML 注释
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable CA1002 // 不要公开泛型列表
#pragma warning disable CA2227 // 集合属性应为只读
using MoAI.Infra.Feishu.Models;
using Refit;

namespace MoAI.Infra.Feishu;

/// <summary>
/// 飞书机器人.
/// </summary>
public interface IFeishuWebHookClient
{
    public HttpClient Client { get; }

    /// <summary>
    /// 推送消息.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="request"></param>
    /// <returns></returns>
    [Post("/open-apis/bot/v2/hook/{key}")]
    Task<FeishuApiResponse> SendPostAsync([Query] string key, [Body(BodySerializationMethod.Serialized)] FeishuWebHookPostRequest request);

    /// <summary>
    /// 推送消息.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="request"></param>
    /// <returns></returns>
    [Post("/open-apis/bot/v2/hook/{key}")]
    Task<FeishuApiResponse> SendTextAsync([Query] string key, [Body(BodySerializationMethod.Serialized)] FeishuWebHookTextRequest request);

    /// <summary>
    /// 推送消息.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="request"></param>
    /// <returns></returns>
    [Post("/open-apis/bot/v2/hook/{key}")]
    Task<FeishuApiResponse> SendShareChatAsync([Query] string key, [Body(BodySerializationMethod.Serialized)] FeishuWebHookShareChatRequest request);

    /// <summary>
    /// 推送消息.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="request"></param>
    /// <returns></returns>
    [Post("/open-apis/bot/v2/hook/{key}")]
    Task<FeishuApiResponse> SendImageAsync([Query] string key, [Body(BodySerializationMethod.Serialized)] FeishuWebHookImageRequest request);

    /// <summary>
    /// 推送消息.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="request"></param>
    /// <returns></returns>
    [Post("/open-apis/bot/v2/hook/{key}")]
    Task<FeishuApiResponse> SendInteractiveAsync([Query] string key, [Body(BodySerializationMethod.Serialized)] FeishuWebHookInteractiveRequest request);
}
