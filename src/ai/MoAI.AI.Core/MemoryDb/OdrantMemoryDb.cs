using Maomi;
using Microsoft.Extensions.Configuration;
using Microsoft.KernelMemory;
using MoAI.AiModel.Services;

namespace MoAI.AI.MemoryDb;

[InjectOnScoped(ServiceKey = "Odrant")]
public class OdrantMemoryDb : IMemoryDbClient
{
    private readonly IConfiguration _configuration;

    public OdrantMemoryDb(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public IKernelMemoryBuilder Configure(IKernelMemoryBuilder kernelMemoryBuilder, string connectionString)
    {
        return kernelMemoryBuilder.WithQdrantMemoryDb(new QdrantConfig
        {
            Endpoint = connectionString,
            APIKey = _configuration["MoAI:Wiki:APIKey"]!,
        });
    }
}
