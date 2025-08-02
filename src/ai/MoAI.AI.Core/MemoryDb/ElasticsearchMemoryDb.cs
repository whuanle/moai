// <copyright file="ElasticsearchMemoryDb.cs" company="MoAI">
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
