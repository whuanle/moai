// <copyright file="SQLServerMemoryDb.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Maomi;
using Microsoft.Extensions.Configuration;
using Microsoft.KernelMemory;
using MoAI.AiModel.Services;
using MoAI.Infra;

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
