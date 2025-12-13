using Microsoft.KernelMemory;

namespace MoAI.AiModel.Services;

// todo： 废弃
/// <summary>
/// 知识库客户端配置器.
/// </summary>
public interface IMemoryDbClient
{
    /// <summary>
    /// 配置知识库客户端.
    /// </summary>
    /// <param name="kernelMemoryBuilder"></param>
    /// <param name="connectionString"></param>
    /// <returns></returns>
    IKernelMemoryBuilder Configure(IKernelMemoryBuilder kernelMemoryBuilder, string connectionString);
}