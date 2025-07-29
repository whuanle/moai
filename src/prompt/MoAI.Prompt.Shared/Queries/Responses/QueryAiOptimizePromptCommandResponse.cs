using MediatR;
using MoAI.AI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoAI.Prompt.Queries.Responses;

public class QueryAiOptimizePromptCommandResponse
{
    /// <summary>
    /// 请求使用量.
    /// </summary>
    public TextTokenUsage Useage { get; init; } = default!;

    /// <summary>
    /// 回复内容.
    /// </summary>
    public string Content { get; init; } = default!;
}