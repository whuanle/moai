using Maomi;
using Microsoft.Extensions.Configuration;
using Microsoft.KernelMemory;
using MoAI.AiModel.Services;

namespace MoAI.AI.MemoryDb;

[InjectOnScoped(ServiceKey = "Elasticsearch")]
public class ElasticsearchMemoryDb : IMemoryDbClient
{
    private readonly IConfiguration _configuration;

    public ElasticsearchMemoryDb(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public IKernelMemoryBuilder Configure(IKernelMemoryBuilder kernelMemoryBuilder, string connectionString)
    {
        return kernelMemoryBuilder.WithElasticsearchMemoryDb(new ElasticsearchConfig
        {
            Endpoint = connectionString,
            IndexPrefix = "moaidocument",
            Password = _configuration["MoAI:Wiki:Password"]!,
            UserName = _configuration["MoAI:Wiki:UserName"]!,
        });
    }
}
