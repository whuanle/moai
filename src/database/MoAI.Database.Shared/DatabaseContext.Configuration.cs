using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Database.Audits;
using MoAI.Database.Entities;
using MoAI.Infra.Helpers;
using MoAI.Infra.Models;
using System.Linq.Expressions;
using System.Text;

namespace MoAI.Database;

/// <summary>
/// 数据库上下文.
/// </summary>
public partial class DatabaseContext
{
    protected partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        SeedData(modelBuilder);

        QueryFilter(modelBuilder);
    }

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

    // 定义种子数据
    protected void SeedData(ModelBuilder modelBuilder)
    {
        const string defaultPassword = "YWJjZDEyMzQ1Ng==";
        var (hashPassword, salt) = PBKDF2Helper.ToHash(Encoding.UTF8.GetString(Convert.FromBase64String(defaultPassword)));

        // 生成用户数据
        modelBuilder.Entity<UserEntity>().HasData(
            new UserEntity
            {
                Id = 1,
                UserName = "admin",
                NickName = "admin",
                Email = "admin@admin.com",
                Password = hashPassword,
                PasswordSalt = salt,
                Phone = "12345678901",
            });

        // 生成系统初始化配置.
        modelBuilder.Entity<SettingEntity>().HasData(
            new SettingEntity
            {
                Id = 1,
                Key = "root",
                Value = "1",
                Description = "超级管理员id"
            });
    }

    // 审计属性过滤
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