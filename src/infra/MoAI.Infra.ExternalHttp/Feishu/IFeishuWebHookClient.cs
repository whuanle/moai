
#pragma warning disable CS1591 // 缺少对公共可见类型或成员的 XML 注释
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable CA1002 // 不要公开泛型列表
#pragma warning disable CA2227 // 集合属性应为只读
using MoAI.Infra.Feishu.Models;
using Refit;

namespace MoAI.Infra.Feishu;

/// <summary>
/// 飞书机器人 Webhook 客户端。路径模板里的 <c>{key}</c> 是机器人 token，由调用方传入.
/// </summary>
public interface IFeishuWebHookClient
{
    /// <summary>
    /// 推送消息.
    /// </summary>
    /// <param name="key">机器人 token（路径段）.</param>
    /// <param name="request"></param>
    [Post("/open-apis/bot/v2/hook/{key}")]
    Task<FeishuCode> SendPostAsync(string key, [Body(BodySerializationMethod.Serialized)] FeishuWebHookPostRequest request);

    /// <summary>
    /// 推送消息.
    /// </summary>
    /// <param name="key">机器人 token（路径段）.</param>
    /// <param name="request"></param>
    [Post("/open-apis/bot/v2/hook/{key}")]
    Task<FeishuCode> SendTextAsync(string key, [Body(BodySerializationMethod.Serialized)] FeishuWebHookTextRequest request);

    /// <summary>
    /// 推送消息.
    /// </summary>
    /// <param name="key">机器人 token（路径段）.</param>
    /// <param name="request"></param>
    [Post("/open-apis/bot/v2/hook/{key}")]
    Task<FeishuCode> SendShareChatAsync(string key, [Body(BodySerializationMethod.Serialized)] FeishuWebHookShareChatRequest request);

    /// <summary>
    /// 推送消息.
    /// </summary>
    /// <param name="key">机器人 token（路径段）.</param>
    /// <param name="request"></param>
    [Post("/open-apis/bot/v2/hook/{key}")]
    Task<FeishuCode> SendImageAsync(string key, [Body(BodySerializationMethod.Serialized)] FeishuWebHookImageRequest request);

    /// <summary>
    /// 推送消息.
    /// </summary>
    /// <param name="key">机器人 token（路径段）.</param>
    /// <param name="request"></param>
    [Post("/open-apis/bot/v2/hook/{key}")]
    Task<FeishuCode> SendInteractiveAsync(string key, [Body(BodySerializationMethod.Serialized)] FeishuWebHookInteractiveRequest request);
}
