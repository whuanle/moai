using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoAI.Prompt.PromptEndpoints.Models;

public class AiOptimizePromptRequest
{
    /// <summary>
    /// AI 模型 id.
    /// </summary>
    public int AiModelId { get; init; }

    /// <summary>
    /// 用户原本的提示词
    /// </summary>
    public string SourcePrompt { get; init; }
}
