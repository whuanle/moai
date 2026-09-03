namespace MoAI.AIPlugin.Contracts;

/// <summary>
/// 静态插件运行时接口。静态插件无需额外配置，所有参数通过请求对象传入.
/// </summary>
/// <typeparam name="TRequest">请求参数类型.</typeparam>
/// <typeparam name="TResponse">响应结果类型.</typeparam>
public interface IStaticPluginRuntime<TRequest, TResponse> : IPluginRuntime<TRequest, TResponse>
{
}
