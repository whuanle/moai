using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Database.Audits;
using MoAI.Database.Seed;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using System.Linq.Expressions;

namespace MoAI.Database;

/// <summary>
/// 数据库上下文.
/// </summary>
public partial class DatabaseContext
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="DatabaseContext"/> class.
    /// </summary>
    /// <param name="options"></param>
    /// <param name="serviceProvider"></param>
    public DatabaseContext(DbContextOptions options, IServiceProvider serviceProvider)
        : base(options)
    {
        _serviceProvider = serviceProvider;

        // 配置过滤器.
        ChangeTracker.Tracked += (state, args) =>
        {
            AuditFilter(args);
        };

        ChangeTracker.StateChanged += (state, args) =>
        {
            AuditFilter(args);
        };
    }

    /// <summary>
    /// 十六进制字符串 byte[] 转换器（SHA256专用）.
    /// </summary>
    private static readonly ValueConverter<string, byte[]> Sha256HexConverter = new ValueConverter<string, byte[]>(
        hex => Convert.FromHexString(hex),
#pragma warning disable CA1308 // 将字符串规范化为大写
        bytes => Convert.ToHexString(bytes).ToLowerInvariant());
#pragma warning restore CA1308 // 将字符串规范化为大写

    /// <summary>
    /// OnModelCreatingPartial.
    /// </summary>
    /// <param name="modelBuilder"></param>
    protected static partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                // 判断条件：CLR类型是string，属性名包含 Sha256
                if (property.ClrType == typeof(string) && property.Name.Contains("sha256", StringComparison.OrdinalIgnoreCase))
                {
                    property.SetValueConverter(Sha256HexConverter);
                    property.SetColumnType("bytea");
                }
            }
        }

        SeedData(modelBuilder);

        QueryFilter(modelBuilder);
    }

    /// <summary>
    /// 查询过滤.
    /// </summary>
    /// <param name="modelBuilder"></param>
    protected static void QueryFilter(ModelBuilder modelBuilder)
    {
        // 给实体配置查询时自动加上 IsDeleted == 0;
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
        UserSeed.Apply(modelBuilder);
        ClassifySeed.Apply(modelBuilder);
        SettingSeed.Apply(modelBuilder);
    }

    /// <summary>
    /// 审计属性过滤.
    /// </summary>
    /// <param name="args"></param>
    protected void AuditFilter(EntityEntryEventArgs args)
    {
        var userContextProvider = _serviceProvider.GetRequiredService<IUserContextProvider>();
        var userContext = userContextProvider.GetUserContext();

        if (args.Entry.State == EntityState.Unchanged)
        {
            return;
        }

        if (args.Entry.State == EntityState.Added && args.Entry.Entity is ICreationAudited creationAudited)
        {
            creationAudited.CreateUserId = userContext?.UserId ?? default(long);
            creationAudited.CreateTime = DateTimeOffset.Now;
            if (args.Entry.Entity is IModificationAudited modificationAudited)
            {
                modificationAudited.UpdateUserId = userContext?.UserId ?? default(long);
                modificationAudited.UpdateTime = DateTimeOffset.Now;
            }
        }
        else if (args.Entry.State == EntityState.Modified && args.Entry.Entity is IModificationAudited modificationAudited)
        {
            if (userContext != null && userContext.UserId != 0)
            {
                modificationAudited.UpdateUserId = userContext.UserId;
            }

            modificationAudited.UpdateTime = DateTimeOffset.Now;
        }
        else if (args.Entry.State == EntityState.Deleted && args.Entry.Entity is IDeleteAudited deleteAudited)
        {
            args.Entry.State = EntityState.Modified;
            deleteAudited.IsDeleted = DateTimeOffset.Now.Ticks;
            deleteAudited.UpdateTime = DateTimeOffset.Now;

            if (userContext != null && userContext.UserId != 0)
            {
                deleteAudited.UpdateUserId = userContext.UserId;
            }
        }
    }
}