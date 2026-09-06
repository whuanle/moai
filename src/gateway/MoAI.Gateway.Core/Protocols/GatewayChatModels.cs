using System.Text.Json;

namespace MoAI.Gateway.Protocols;

/// <summary>
/// 网关消息角色.
/// </summary>
public enum GatewayRole
{
    /// <summary>
    /// 系统提示.
    /// </summary>
    System,

    /// <summary>
    /// 用户.
    /// </summary>
    User,

    /// <summary>
    /// 助手.
    /// </summary>
    Assistant,

    /// <summary>
    /// 工具结果.
    /// </summary>
    Tool,
}

/// <summary>
/// 结束原因.
/// </summary>
public enum GatewayFinishReason
{
    /// <summary>
    /// 正常结束.
    /// </summary>
    Stop,

    /// <summary>
    /// 达到最大输出.
    /// </summary>
    Length,

    /// <summary>
    /// 需要调用工具.
    /// </summary>
    ToolCalls,

    /// <summary>
    /// 内容被过滤.
    /// </summary>
    ContentFilter,

    /// <summary>
    /// 其它.
    /// </summary>
    Other,
}

/// <summary>
/// 工具调用选择模式.
/// </summary>
public enum GatewayToolChoiceMode
{
    /// <summary>
    /// 由模型自行决定.
    /// </summary>
    Auto,

    /// <summary>
    /// 禁用工具.
    /// </summary>
    None,

    /// <summary>
    /// 必须调用某个工具.
    /// </summary>
    Required,

    /// <summary>
    /// 指定函数.
    /// </summary>
    Function,
}

/// <summary>
/// 内容片段基类.
/// </summary>
public abstract class GatewayContentPart
{
}

/// <summary>
/// 文本片段.
/// </summary>
public sealed class GatewayTextPart : GatewayContentPart
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GatewayTextPart"/> class.
    /// </summary>
    /// <param name="text">文本.</param>
    public GatewayTextPart(string text)
    {
        Text = text;
    }

    /// <summary>
    /// 文本内容.
    /// </summary>
    public string Text { get; set; }
}

/// <summary>
/// 图片片段，Url 为 http(s):// 或 data:mime;base64,xxx.
/// </summary>
public sealed class GatewayImagePart : GatewayContentPart
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GatewayImagePart"/> class.
    /// </summary>
    /// <param name="url">图片地址或 data URI.</param>
    public GatewayImagePart(string url)
    {
        Url = url;
    }

    /// <summary>
    /// 图片地址.
    /// </summary>
    public string Url { get; set; }
}

/// <summary>
/// 工具调用.
/// </summary>
public sealed class GatewayToolCall
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GatewayToolCall"/> class.
    /// </summary>
    /// <param name="id">调用 id.</param>
    /// <param name="name">函数名.</param>
    /// <param name="argumentsJson">参数 JSON 字符串.</param>
    public GatewayToolCall(string id, string name, string argumentsJson)
    {
        Id = id;
        Name = name;
        ArgumentsJson = argumentsJson;
    }

    /// <summary>
    /// 调用 id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// 函数名.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 参数 JSON 字符串，空对象时为 "{}".
    /// </summary>
    public string ArgumentsJson { get; set; }
}

/// <summary>
/// 工具定义.
/// </summary>
public sealed class GatewayToolDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GatewayToolDefinition"/> class.
    /// </summary>
    /// <param name="name">函数名.</param>
    public GatewayToolDefinition(string name)
    {
        Name = name;
    }

    /// <summary>
    /// 函数名.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 描述.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 参数 JSON Schema.
    /// </summary>
    public JsonElement? ParametersSchema { get; set; }
}

/// <summary>
/// 工具选择.
/// </summary>
public sealed class GatewayToolChoice
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GatewayToolChoice"/> class.
    /// </summary>
    /// <param name="mode">模式.</param>
    /// <param name="functionName">mode=Function 时的函数名.</param>
    public GatewayToolChoice(GatewayToolChoiceMode mode, string? functionName = null)
    {
        Mode = mode;
        FunctionName = functionName;
    }

    /// <summary>
    /// 模式.
    /// </summary>
    public GatewayToolChoiceMode Mode { get; set; }

    /// <summary>
    /// 指定函数名.
    /// </summary>
    public string? FunctionName { get; set; }
}

/// <summary>
/// token 用量.
/// </summary>
public sealed class GatewayUsage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GatewayUsage"/> class.
    /// </summary>
    /// <param name="promptTokens">输入.</param>
    /// <param name="completionTokens">输出.</param>
    public GatewayUsage(int promptTokens, int completionTokens)
    {
        PromptTokens = promptTokens;
        CompletionTokens = completionTokens;
        TotalTokens = promptTokens + completionTokens;
    }

    /// <summary>
    /// 输入 tokens.
    /// </summary>
    public int PromptTokens { get; set; }

    /// <summary>
    /// 输出 tokens.
    /// </summary>
    public int CompletionTokens { get; set; }

    /// <summary>
    /// 总 tokens.
    /// </summary>
    public int TotalTokens { get; set; }
}

/// <summary>
/// 消息.
/// </summary>
public sealed class GatewayMessage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GatewayMessage"/> class.
    /// </summary>
    /// <param name="role">角色.</param>
    /// <param name="text">纯文本内容，可与 Parts 共存.</param>
    public GatewayMessage(GatewayRole role, string? text = null)
    {
        Role = role;
        Text = text;
    }

    /// <summary>
    /// 角色.
    /// </summary>
    public GatewayRole Role { get; set; }

    /// <summary>
    /// 纯文本内容.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// 多模态片段，null 表示纯文本消息.
    /// </summary>
    public List<GatewayContentPart>? Parts { get; set; }

    /// <summary>
    /// 助手消息携带的工具调用.
    /// </summary>
    public List<GatewayToolCall>? ToolCalls { get; set; }

    /// <summary>
    /// Tool 角色消息对应的工具调用 id.
    /// </summary>
    public string? ToolCallId { get; set; }

    /// <summary>
    /// 工具名（部分协议需要）.
    /// </summary>
    public string? Name { get; set; }
}

/// <summary>
/// 归一化后的对话请求（网关中间表示）.
/// </summary>
public sealed class GatewayChatRequest
{
    /// <summary>
    /// 模型名.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// 消息列表.
    /// </summary>
    public List<GatewayMessage> Messages { get; } = new();

    /// <summary>
    /// 工具定义，null=未携带.
    /// </summary>
    public List<GatewayToolDefinition>? Tools { get; set; }

    /// <summary>
    /// 工具选择，null=协议默认（auto）.
    /// </summary>
    public GatewayToolChoice? ToolChoice { get; set; }

    /// <summary>
    /// 温度.
    /// </summary>
    public double? Temperature { get; set; }

    /// <summary>
    /// top_p.
    /// </summary>
    public double? TopP { get; set; }

    /// <summary>
    /// 最大输出 tokens.
    /// </summary>
    public int? MaxOutputTokens { get; set; }

    /// <summary>
    /// 停止序列.
    /// </summary>
    public List<string>? StopSequences { get; set; }

    /// <summary>
    /// 是否流式.
    /// </summary>
    public bool Stream { get; set; }

    /// <summary>
    /// 终端用户标识.
    /// </summary>
    public string? User { get; set; }
}

/// <summary>
/// 归一化后的对话响应.
/// </summary>
public sealed class GatewayChatResponse
{
    /// <summary>
    /// 响应 id.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 模型名.
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// 文本内容.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// 工具调用.
    /// </summary>
    public List<GatewayToolCall> ToolCalls { get; } = new();

    /// <summary>
    /// 结束原因.
    /// </summary>
    public GatewayFinishReason FinishReason { get; set; } = GatewayFinishReason.Stop;

    /// <summary>
    /// 用量.
    /// </summary>
    public GatewayUsage? Usage { get; set; }
}

/// <summary>
/// 流式事件（网关中间表示）.
/// </summary>
public abstract class GatewayStreamEvent
{
    /// <summary>
    /// 流开始.
    /// </summary>
    public sealed class Started : GatewayStreamEvent
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Started"/> class.
        /// </summary>
        /// <param name="id">响应 id.</param>
        /// <param name="model">模型名.</param>
        public Started(string id, string model)
        {
            Id = id;
            Model = model;
        }

        /// <summary>
        /// 响应 id.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// 模型名.
        /// </summary>
        public string Model { get; }
    }

    /// <summary>
    /// 文本增量.
    /// </summary>
    public sealed class TextDelta : GatewayStreamEvent
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TextDelta"/> class.
        /// </summary>
        /// <param name="text">增量文本.</param>
        public TextDelta(string text)
        {
            Text = text;
        }

        /// <summary>
        /// 增量文本.
        /// </summary>
        public string Text { get; }
    }

    /// <summary>
    /// 工具调用开始.
    /// </summary>
    public sealed class ToolCallStarted : GatewayStreamEvent
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ToolCallStarted"/> class.
        /// </summary>
        /// <param name="index">工具调用序号（请求内自增）.</param>
        /// <param name="id">调用 id.</param>
        /// <param name="name">函数名.</param>
        public ToolCallStarted(int index, string id, string name)
        {
            Index = index;
            Id = id;
            Name = name;
        }

        /// <summary>
        /// 工具调用序号.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// 调用 id.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// 函数名.
        /// </summary>
        public string Name { get; }
    }

    /// <summary>
    /// 工具调用参数增量.
    /// </summary>
    public sealed class ToolCallArgumentsDelta : GatewayStreamEvent
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ToolCallArgumentsDelta"/> class.
        /// </summary>
        /// <param name="index">工具调用序号.</param>
        /// <param name="argumentsDelta">参数增量 JSON 片段.</param>
        public ToolCallArgumentsDelta(int index, string argumentsDelta)
        {
            Index = index;
            ArgumentsDelta = argumentsDelta;
        }

        /// <summary>
        /// 工具调用序号.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// 参数增量.
        /// </summary>
        public string ArgumentsDelta { get; }
    }

    /// <summary>
    /// 流结束.
    /// </summary>
    public sealed class Finished : GatewayStreamEvent
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Finished"/> class.
        /// </summary>
        /// <param name="reason">结束原因.</param>
        /// <param name="usage">用量，可能为 null（由网关估算）.</param>
        public Finished(GatewayFinishReason reason, GatewayUsage? usage)
        {
            Reason = reason;
            Usage = usage;
        }

        /// <summary>
        /// 结束原因.
        /// </summary>
        public GatewayFinishReason Reason { get; }

        /// <summary>
        /// 用量.
        /// </summary>
        public GatewayUsage? Usage { get; }
    }
}
