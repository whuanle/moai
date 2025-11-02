using Maomi;
using Microsoft.KernelMemory;
using MoAI.AiModel.Services;

namespace MoAI.AI.MemoryDb;

[InjectOnScoped(ServiceKey = "SQLServer")]
public class SQLServerMemoryDb : IMemoryDbClient
{
    public IKernelMemoryBuilder Configure(IKernelMemoryBuilder kernelMemoryBuilder, string connectionString)
    {
        return kernelMemoryBuilder.WithSqlServerMemoryDb(new Microsoft.KernelMemory.MemoryDb.SQLServer.SqlServerConfig
        {
            ConnectionString = connectionString,
        });
    }
}
