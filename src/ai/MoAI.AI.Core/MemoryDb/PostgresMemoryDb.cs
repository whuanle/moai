// <copyright file="PostgresMemoryDb.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

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
