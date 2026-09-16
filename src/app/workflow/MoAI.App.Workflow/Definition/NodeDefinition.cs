using System.Text.Json;

namespace MoAI.App.Workflow.Definition;

/// <summary>
/// 节点定义模型 - 前端设计器中一个节点的完整配置.
/// </summary>
public class NodeDefinition
{
    /// <summary>
    /// 节点唯一标识符（前端生成，如 node_1a2b3c），后续节点通过它引用输出变量.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 节点显示名称.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 节点类型，见 <see cref="NodeTypes"/>；使用字符串以便扩展自定义节点类型.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 节点描述.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 节点私有配置（如插件节点的 pluginKey、AI 节点的 model、JS 节点的 code），由各节点运行时自行解析.
    /// 默认值为 Undefined，读取时请先判断 <see cref="JsonElement.ValueKind"/> 是否为 Object.
    /// </summary>
    public JsonElement Config { get; set; }

    /// <summary>
    /// 获取配置对象，若未配置则返回空对象.
    /// </summary>
    public JsonElement GetConfig()
    {
        return Config.ValueKind == JsonValueKind.Object ? Config : JsonDocument.Parse("{}").RootElement;
    }

    /// <summary>
    /// 输入字段绑定，键为字段名称，值为取值表达式.
    /// 执行时由数据传输模块解析为节点的实际输入.
    /// </summary>
    public Dictionary<string, FieldBinding> Inputs { get; set; } = new();

    /// <summary>
    /// 输出字段定义，描述节点会产生哪些输出（供设计器展示可选变量，启动参数校验等）.
    /// </summary>
    public List<PortDefinition> Outputs { get; set; } = new();
}
