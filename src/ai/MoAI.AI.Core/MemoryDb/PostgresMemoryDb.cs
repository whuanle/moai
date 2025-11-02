using Maomi;
using Microsoft.Extensions.Configuration;
using Microsoft.KernelMemory;
using MoAI.AiModel.Services;

namespace MoAI.AI.MemoryDb;

[InjectOnScoped(ServiceKey = "Porstgres")]
public class PostgresMemoryDb : IMemoryDbClient
{
    private readonly IConfiguration _configuration;

    public PostgresMemoryDb(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public IKernelMemoryBuilder Configure(IKernelMemoryBuilder kernelMemoryBuilder, string connectionString)
    {
        return kernelMemoryBuilder.WithPostgresMemoryDb(new PostgresConfig
        {
            ConnectionString = connectionString,
            TableNamePrefix = "wiki_",
        });
    }
}
