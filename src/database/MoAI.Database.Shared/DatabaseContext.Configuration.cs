// <copyright file="DatabaseContext.Configuration.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Database.Audits;
using MoAI.Database.Entities;
using MoAI.Database.Models;
using MoAI.Infra.Helpers;
using MoAI.Infra.Models;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace MoAI.Database;

/// <summary>
/// 数据库上下文.
/// </summary>
public partial class DatabaseContext
{
    /// <summary>
    /// OnModelCreatingPartial.
    /// </summary>
    /// <param name="modelBuilder"></param>
    protected static partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        SeedData(modelBuilder);

        QueryFilter(modelBuilder);
    }

    /// <summary>
    /// 查询过滤.
    /// </summary>
    /// <param name="modelBuilder"></param>
    protected static void QueryFilter(ModelBuilder modelBuilder)
    {
        // 给实体配置查询时自动加上 IsDeleted == false;
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.ClrType.IsAssignableTo(typeof(IDeleteAudited)))
            {
                // 构造 x => x.IsDeleted == 0
                var parameter = Expression.Parameter(entityType.ClrType, "x");
                MemberExpression property = Expression.Property(parameter, nameof(IDeleteAudited.IsDeleted));
                ConstantExpression constant = Expression.Constant(0L);
                BinaryExpression comparison = Expression.Equal(property, constant);

                var lambdaExpression = Expression.Lambda(comparison, parameter);

                entityType.SetQueryFilter(lambdaExpression);
            }
        }
    }

    /// <summary>
    /// 定义种子数据.
    /// </summary>
    /// <param name="modelBuilder"></param>
    protected static void SeedData(ModelBuilder modelBuilder)
    {
        const string defaultPassword = "YWJjZDEyMzQ1Ng==";
        var (hashPassword, salt) = PBKDF2Helper.ToHash(Encoding.UTF8.GetString(Convert.FromBase64String(defaultPassword)));

        // 生成用户数据
        modelBuilder.Entity<UserEntity>().HasData(
            new UserEntity
            {
                Id = SystemSettingKeys.RootValue,
                UserName = "admin",
                NickName = "admin",
                Email = "admin@admin.com",
                Password = hashPassword,
                PasswordSalt = salt,
                Phone = "12345678901",
                IsAdmin = true
            });

        // 生成系统初始化配置.
        var fields = typeof(SystemSettingKeys).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        var fieldDictionary = fields.ToDictionary(x => x.Name, x => x);

        int id = 1;
        foreach (var field in fields.Where(x => !x.Name.EndsWith("Value", StringComparison.CurrentCultureIgnoreCase)))
        {
            var valueField = fieldDictionary.GetValueOrDefault($"{field.Name}Value");
            if (valueField == null)
            {
                continue;
            }

            var key = field.GetValue(null)?.ToString() ?? string.Empty;
            var value = valueField.GetValue(null)?.ToString() ?? string.Empty;

            var descriptionAttribute = field.GetCustomAttribute<DescriptionAttribute>();

            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value) || descriptionAttribute == null)
            {
                continue;
            }

            modelBuilder.Entity<SettingEntity>().HasData(
                new SettingEntity
                {
                    Id = id,
                    Key = key,
                    Value = value,
                    Description = descriptionAttribute.Description
                });

            id++;
        }
    }

    /// <summary>
    /// 审计属性过滤.
    /// </summary>
    /// <param name="args"></param>
    protected void AuditFilter(EntityEntryEventArgs args)
    {
        var userContext = _serviceProvider.GetService<UserContext>();

        if (args.Entry.State == EntityState.Unchanged)
        {
            return;
        }

        if (args.Entry.State == EntityState.Added && args.Entry.Entity is ICreationAudited creationAudited)
        {
            creationAudited.CreateUserId = userContext?.UserId ?? default(int);
            creationAudited.CreateTime = DateTimeOffset.Now;
            if (args.Entry.Entity is IModificationAudited modificationAudited)
            {
                modificationAudited.UpdateUserId = userContext?.UserId ?? default(int);
                modificationAudited.UpdateTime = DateTimeOffset.Now;
            }
        }
        else if (args.Entry.State == EntityState.Modified && args.Entry.Entity is IModificationAudited modificationAudited)
        {
            modificationAudited.UpdateUserId = userContext?.UserId ?? default(int);
            modificationAudited.UpdateTime = DateTimeOffset.Now;
        }
        else if (args.Entry.State == EntityState.Deleted && args.Entry.Entity is IDeleteAudited deleteAudited)
        {
            args.Entry.State = EntityState.Modified;

            deleteAudited.IsDeleted = DateTimeOffset.Now.Ticks;
            deleteAudited.UpdateUserId = userContext?.UserId ?? default(int);
            deleteAudited.UpdateTime = DateTimeOffset.Now;
        }
    }
}