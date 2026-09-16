using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using MoAI.App.Workflow.Definition;

namespace MoAI.App.Workflow;

/// <summary>
/// 工作流 JSON 序列化配置 - 前端设计 JSON 与引擎模型的统一序列化契约：
/// camelCase 属性名，枚举序列化为字符串（ExpressionType 等使用 JsonStringEnumMemberName 定义的名称）.
/// </summary>
public static class WorkflowJson
{
    /// <summary>
    /// 序列化选项.
    /// </summary>
    public static JsonSerializerOptions Options { get; } = Create();

    /// <summary>
    /// 反序列化工作流定义.
    /// </summary>
    public static WorkflowDefinition DeserializeDefinition(string json)
    {
        var definition = JsonSerializer.Deserialize<WorkflowDefinition>(json, Options);
        if (definition == null)
        {
            throw new WorkflowException("工作流定义 JSON 反序列化结果为空");
        }

        return definition;
    }

    /// <summary>
    /// 序列化工作流定义.
    /// </summary>
    public static string SerializeDefinition(WorkflowDefinition definition)
    {
        return JsonSerializer.Serialize(definition, Options);
    }

    /// <summary>
    /// 创建序列化选项.
    /// </summary>
    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,

            // 中文等非 ASCII 字符不转义，便于阅读与前端直接使用
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        options.Converters.Add(new UndefinedJsonElementConverter());
        return options;
    }

    /// <summary>
    /// JsonElement 转换器：未赋值（Undefined）的 Config 序列化为 null，避免默认 JsonElement 无法写出.
    /// </summary>
    private class UndefinedJsonElementConverter : JsonConverter<JsonElement>
    {
        /// <inheritdoc/>
        public override void Write(Utf8JsonWriter writer, JsonElement value, JsonSerializerOptions options)
        {
            if (value.ValueKind == JsonValueKind.Undefined)
            {
                writer.WriteNullValue();
            }
            else
            {
                value.WriteTo(writer);
            }
        }

        /// <inheritdoc/>
        public override JsonElement Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return JsonElement.ParseValue(ref reader);
        }
    }
}
