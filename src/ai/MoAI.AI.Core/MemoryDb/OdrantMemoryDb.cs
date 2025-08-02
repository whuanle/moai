// <copyright file="OdrantMemoryDb.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

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
