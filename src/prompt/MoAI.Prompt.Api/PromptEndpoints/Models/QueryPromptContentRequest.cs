using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoAI.Prompt.PromptEndpoints.Models;

public class QueryPromptContentRequest
{
    /// <summary>
    /// 提示词 id.
    /// </summary>
    public int PromptId { get; init; }
}
