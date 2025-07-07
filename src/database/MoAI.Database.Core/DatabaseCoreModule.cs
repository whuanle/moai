// <copyright file="DatabaseCoreModule.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Maomi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Infra;
using StackExchange.Redis.Extensions.Core;
using StackExchange.Redis.Extensions.Core.Abstractions;
using StackExchange.Redis.Extensions.Core.Configuration;
using StackExchange.Redis.Extensions.Core.Implementations;
using StackExchange.Redis.Extensions.System.Text.Json;

namespace MoAI.Database;

/// <summary>
/// DatabaseCoreModule.
/// </summary>
// [InjectModule<DatabasePostgresModule>]
[InjectModule<DatabaseMysqlModule>]
public class DatabaseCoreModule : IModule
{
    private readonly SystemOptions _systemOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseCoreModule"/> class.
    /// </summary>
    /// <param name="systemOptions"></param>
    public DatabaseCoreModule(SystemOptions systemOptions)
    {
        _systemOptions = systemOptions!;
    }

    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        // 添加 redis
        AddStackExchangeRedis(context.Services, new RedisConfiguration
        {
            ConnectionString = _systemOptions.Redis,
            PoolSize = 10,
            KeyPrefix = "maomi:",
            ConnectTimeout = 5000,
            IsDefault = true
        });

        //// 如果使用内存数据库
        // if ("inmemory".Equals(systemOptions.DBType, StringComparison.OrdinalIgnoreCase))
        // {
        //    DatabaseOptions? dbContextOptions = new()
        //    {
        //        ConfigurationAssembly = typeof(DatabaseCoreModule).Assembly,
        //        EntityAssembly = typeof(DatabaseContext).Assembly
        //    };

        // context.Services.AddSingleton(dbContextOptions);

        // // 注册内存数据库
        //    context.Services.AddDbContext<DatabaseContext>(options =>
        //    {
        //        options.UseInMemoryDatabase(systemOptions.Database);
        //    });

        // // 创建数据库
        //    using ServiceProvider? serviceProvider = context.Services.BuildServiceProvider();
        //    using IServiceScope? scope = serviceProvider.CreateScope();
        //    DatabaseContext? dbContext = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        //    dbContext.Database.EnsureCreated();
        // }
    }

    private static void AddStackExchangeRedis(IServiceCollection services, RedisConfiguration redisConfiguration)
    {
        services.AddSingleton<ISerializer, SystemTextJsonSerializer>();

        services.AddSingleton<IRedisClientFactory, RedisClientFactory>();

        services.AddSingleton((provider) => provider
            .GetRequiredService<IRedisClientFactory>()
            .GetDefaultRedisClient()
            .GetDefaultDatabase());

        services.AddSingleton(redisConfiguration);
    }
}