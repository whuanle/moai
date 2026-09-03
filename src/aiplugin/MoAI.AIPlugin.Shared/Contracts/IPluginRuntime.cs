using System;
using System.Threading;
using System.Threading.Tasks;

namespace MoAI.AIPlugin.Contracts;

/// <summary>
/// 工具插件通用运行时接口，所有插件均需实现.
/// </summary>
/// <typeparam name="TRequest">请求参数类型.</typeparam>
/// <typeparam name="TResponse">响应结果类型.</typeparam>
public interface IPluginRuntime<TRequest, TResponse>
{
    /// <summary>
    /// 获取请求参数的示例 JSON 字符串.
    /// </summary>
    /// <returns>示例 JSON 字符串.</returns>
    static abstract string GetParamsExampleValue();

    /// <summary>
    /// 运行插件.
    /// </summary>
    /// <param name="request">请求参数.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>响应结果.</returns>
    Task<TResponse> RunAsync(TRequest request, CancellationToken cancellationToken);
}
