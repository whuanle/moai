using System.Text.Json.Serialization;

namespace MoAI.Workflow.Models;

public enum WorkflowOutputExpressionType
{
    /// <summary>
    /// 无输出表达式，一般不可选，或忽略处理.
    /// </summary>
    [JsonPropertyName("ignore")]
    Ignore,

    /// <summary>
    /// 固定输出表达式，也就是每个字段可以使用 JsonPath 、字符串插值、正则表达式或者其它形式.
    /// </summary>
    Fixed,

    /// <summary>
    /// 动态输出，不处理字段，节点输出结果直接作为下一个节点的输入，不过用户可以定义结构以便下个节点方便使用变量.
    /// </summary>
    [JsonPropertyName("dynamic")]
    Dynamic,

    /// <summary>
    /// 使用 JavaScript 脚本执行后输出动态内容，不过用户可以定义结构以便下个节点方便使用变量.
    /// </summary>
    JavaScript,
}
