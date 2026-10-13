#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。

using MoAI.Infra.Exceptions;

namespace MoAI.Infra.Jina.Models;

/// <summary>
/// Jina AI 接口统一响应包裹（Search 与 Reader 共用）：<c>{ code, status, data, ... }</c>，
/// <c>code == 200</c> 表示业务成功；错误时错误信息在 <c>message</c>/<c>readable_message</c>.
/// </summary>
/// <typeparam name="T">data 字段类型，搜索为结果数组，阅读为单条结果对象.</typeparam>
public class JinaEnvelope<T>
{
    /// <summary>
    /// 业务状态码，200 表示成功.
    /// </summary>
    public int? Code { get; init; }

    /// <summary>
    /// 平台状态码（如 20000000、42904000），用于细分错误原因.
    /// </summary>
    public int? Status { get; init; }

    /// <summary>
    /// 是否为错误响应.
    /// </summary>
    public bool? Error { get; init; }

    /// <summary>
    /// 错误信息.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// 面向用户的可读错误信息.
    /// </summary>
    public string? ReadableMessage { get; init; }

    /// <summary>
    /// 响应数据.
    /// </summary>
    public T? Data { get; init; }

    /// <summary>
    /// HTTP 200 但业务 code 非 200 时抛出带响应内容的业务异常（额度耗尽、参数不合法等）.
    /// </summary>
    /// <param name="apiName">接口显示名，用于错误提示.</param>
    public void EnsureSuccess(string apiName)
    {
        if (Code == null || Code == 200)
        {
            return;
        }

        var message = ReadableMessage ?? Message;
        throw new BusinessException((int)Code, $"{apiName}调用失败（code={Code}）：{message ?? "未知错误"}");
    }
}
