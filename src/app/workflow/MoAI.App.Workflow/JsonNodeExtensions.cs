using System.Text.Json.Nodes;

namespace MoAI.App.Workflow;

/// <summary>
/// JSON 节点扩展.
/// </summary>
public static class JsonNodeExtensions
{
    /// <summary>
    /// 深拷贝 JSON 对象（<see cref="JsonNode.DeepClone"/> 的返回值是基类 JsonNode，此方法还原为 JsonObject）.
    /// </summary>
    public static JsonObject CloneObject(this JsonObject jsonObject)
    {
        return (JsonObject)jsonObject.DeepClone();
    }
}
