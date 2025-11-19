#pragma warning disable CA1822 // 将成员标记为 static

namespace MoAI.Plugin.Plugins;

/// <summary>
/// 工具插件接口.
/// </summary>
public interface IToolPluginRuntime
{
    /// <summary>
    /// 获取参数示例值 json 字符串.
    /// </summary>
    /// <returns></returns>
    Task<string> GetParamsExampleValue();

    /// <summary>
    /// 使用参数运行测试.
    /// </summary>
    /// <param name="params"></param>
    /// <returns></returns>
    Task<string> TestAsync(string @params);
}
