using Microsoft.SemanticKernel;
using MoAI.AI.Models;

namespace MoAI.AI.Abstract;

// todo： 删除
/// <summary>
/// 构建聊天完成配置器接口.
/// </summary>
public interface IChatCompletionConfigurator
{
    /// <summary>
    /// 配置客户端.
    /// </summary>
    /// <param name="kernelBuilder"></param>
    /// <param name="endpoint"></param>
    /// <returns></returns>
    IKernelBuilder Configure(IKernelBuilder kernelBuilder, AiEndpoint endpoint);
}
