namespace MoAI.Variable.Commands.Responses;

/// <summary>
/// 变量替换响应.
/// </summary>
public class SubstituteVariableCommandResponse
{
    /// <summary>
    /// 替换后的文本；未匹配到变量的 <c>${key}</c> 保留原文.
    /// </summary>
    public string Content { get; set; } = default!;
}
