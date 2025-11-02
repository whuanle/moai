using Maomi;
using Microsoft.KernelMemory;
using MoAI.AiModel.Services;

namespace MoAI.AI.MemoryDb;

[InjectOnScoped(ServiceKey = "Redis")]
public class RedisMemoryDb : IMemoryDbClient
{
    public IKernelMemoryBuilder Configure(IKernelMemoryBuilder kernelMemoryBuilder, string connectionString)
    {
        return kernelMemoryBuilder.WithRedisMemoryDb(connectionString);
    }
}
