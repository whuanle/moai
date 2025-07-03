// <copyright file="MysqlDatabaseContext.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Microsoft.EntityFrameworkCore;

namespace MoAI.Database;

public class MysqlDatabaseContext : DatabaseContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MysqlDatabaseContext"/> class.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="serviceProvider"></param>
    public MysqlDatabaseContext(DbContextOptions options, IServiceProvider serviceProvider)
        : base(options, serviceProvider)
    {
    }

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .ApplyConfigurationsFromAssembly(typeof(MysqlDatabaseContext).Assembly)
            .ApplyConfigurationsFromAssembly(typeof(DatabaseContext).Assembly);

        modelBuilder
            .UseCollation("utf8mb4_general_ci")
            .HasCharSet("utf8mb4");

        base.OnModelCreating(modelBuilder);
    }
}
