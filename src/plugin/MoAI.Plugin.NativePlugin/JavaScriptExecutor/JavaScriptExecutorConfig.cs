#pragma warning disable CA1822 // 将成员标记为 static
#pragma warning disable CA1031 // 不捕获常规异常类型
#pragma warning disable SA1118 // Parameter should not span multiple lines

using MoAI.Plugin.Attributes;
using MoAI.Plugin.Plugins;
using System.Text.Json.Serialization;

namespace MoAI.Plugin.JavaScriptExecutor;

/// <summary>
/// JavaScript 执行参数.
/// </summary>
public class JavaScriptExecutorConfig
{
    /// <summary>
    /// 要执行的代码.
    /// </summary>
    [JsonPropertyName(nameof(JavaScriptCode))]
    [NativePluginField(
        Key = nameof(JavaScriptCode),
        Description = "JavaScript 代码",
        FieldType = PluginConfigFieldType.Code,
        IsRequired = true,
        ExampleValue =
        """
        // 函数必须以 run 命名，paramter 参数是字符串
        // 返回值可以是对象或字符串等类型
        function run(paramter) {
        var obj = JSON.parse(paramter);
        const result = {
            id: obj.id,
            name: "test"
        };
        
        return result;
        }
        """)]
    public string JavaScriptCode { get; set; } = string.Empty;
}