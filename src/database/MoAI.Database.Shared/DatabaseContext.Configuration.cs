using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using MoAI.Database.Audits;
using MoAI.Database.Entities;
using MoAI.Infra.Extensions;
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

        // 插入超级管理员
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
                IsAdmin = true
            });

        modelBuilder.Entity<ClassifyEntity>().HasData(
            new ClassifyEntity { Id = 1, Type = "prompt", Name = "职业" },
            new ClassifyEntity { Id = 2, Type = "prompt", Name = "商业" },
            new ClassifyEntity { Id = 3, Type = "prompt", Name = "工具" },
            new ClassifyEntity { Id = 4, Type = "prompt", Name = "语言" },
            new ClassifyEntity { Id = 5, Type = "prompt", Name = "办公" },
            new ClassifyEntity { Id = 6, Type = "prompt", Name = "通用" },
            new ClassifyEntity { Id = 7, Type = "prompt", Name = "写作" },
            new ClassifyEntity { Id = 8, Type = "prompt", Name = "精选" },
            new ClassifyEntity { Id = 9, Type = "prompt", Name = "编程" },
            new ClassifyEntity { Id = 10, Type = "prompt", Name = "情感" },
            new ClassifyEntity { Id = 11, Type = "prompt", Name = "教育" },
            new ClassifyEntity { Id = 12, Type = "prompt", Name = "创意" },
            new ClassifyEntity { Id = 13, Type = "prompt", Name = "学术" },
            new ClassifyEntity { Id = 14, Type = "prompt", Name = "设计" },
            new ClassifyEntity { Id = 15, Type = "prompt", Name = "艺术" },
            new ClassifyEntity { Id = 16, Type = "prompt", Name = "娱乐" },
            new ClassifyEntity { Id = 17, Type = "prompt", Name = "生活" },
            new ClassifyEntity { Id = 18, Type = "prompt", Name = "医疗" },
            new ClassifyEntity { Id = 19, Type = "prompt", Name = "游戏" },
            new ClassifyEntity { Id = 20, Type = "prompt", Name = "翻译" },
            new ClassifyEntity { Id = 21, Type = "prompt", Name = "音乐" },
            new ClassifyEntity { Id = 22, Type = "prompt", Name = "点评" },
            new ClassifyEntity { Id = 23, Type = "prompt", Name = "文案" },
            new ClassifyEntity { Id = 24, Type = "prompt", Name = "百科" },
            new ClassifyEntity { Id = 25, Type = "prompt", Name = "健康" },
            new ClassifyEntity { Id = 26, Type = "prompt", Name = "营销" },
            new ClassifyEntity { Id = 27, Type = "prompt", Name = "科学" },
            new ClassifyEntity { Id = 28, Type = "prompt", Name = "分析" },
            new ClassifyEntity { Id = 29, Type = "prompt", Name = "法律" },
            new ClassifyEntity { Id = 30, Type = "prompt", Name = "咨询" },
            new ClassifyEntity { Id = 31, Type = "prompt", Name = "金融" },
            new ClassifyEntity { Id = 32, Type = "prompt", Name = "旅游" },
            new ClassifyEntity { Id = 33, Type = "prompt", Name = "管理" });

        modelBuilder.Entity<ClassifyEntity>().HasData(
            new ClassifyEntity { Id = 101, Type = "plugin", Name = "职业" },
            new ClassifyEntity { Id = 102, Type = "plugin", Name = "商业" },
            new ClassifyEntity { Id = 103, Type = "plugin", Name = "工具" },
            new ClassifyEntity { Id = 104, Type = "plugin", Name = "语言" },
            new ClassifyEntity { Id = 105, Type = "plugin", Name = "办公" },
            new ClassifyEntity { Id = 106, Type = "plugin", Name = "通用" },
            new ClassifyEntity { Id = 107, Type = "plugin", Name = "写作" },
            new ClassifyEntity { Id = 108, Type = "plugin", Name = "精选" },
            new ClassifyEntity { Id = 109, Type = "plugin", Name = "编程" },
            new ClassifyEntity { Id = 110, Type = "plugin", Name = "情感" },
            new ClassifyEntity { Id = 111, Type = "plugin", Name = "教育" },
            new ClassifyEntity { Id = 112, Type = "plugin", Name = "创意" },
            new ClassifyEntity { Id = 113, Type = "plugin", Name = "学术" },
            new ClassifyEntity { Id = 114, Type = "plugin", Name = "设计" },
            new ClassifyEntity { Id = 115, Type = "plugin", Name = "艺术" },
            new ClassifyEntity { Id = 116, Type = "plugin", Name = "娱乐" },
            new ClassifyEntity { Id = 117, Type = "plugin", Name = "生活" },
            new ClassifyEntity { Id = 118, Type = "plugin", Name = "医疗" },
            new ClassifyEntity { Id = 119, Type = "plugin", Name = "游戏" },
            new ClassifyEntity { Id = 120, Type = "plugin", Name = "翻译" },
            new ClassifyEntity { Id = 121, Type = "plugin", Name = "音乐" },
            new ClassifyEntity { Id = 122, Type = "plugin", Name = "点评" },
            new ClassifyEntity { Id = 123, Type = "plugin", Name = "文案" },
            new ClassifyEntity { Id = 124, Type = "plugin", Name = "百科" },
            new ClassifyEntity { Id = 125, Type = "plugin", Name = "健康" },
            new ClassifyEntity { Id = 126, Type = "plugin", Name = "营销" },
            new ClassifyEntity { Id = 127, Type = "plugin", Name = "科学" },
            new ClassifyEntity { Id = 128, Type = "plugin", Name = "分析" },
            new ClassifyEntity { Id = 129, Type = "plugin", Name = "法律" },
            new ClassifyEntity { Id = 130, Type = "plugin", Name = "咨询" },
            new ClassifyEntity { Id = 131, Type = "plugin", Name = "金融" },
            new ClassifyEntity { Id = 132, Type = "plugin", Name = "旅游" },
            new ClassifyEntity { Id = 133, Type = "plugin", Name = "管理" });

        // 生成系统初始化配置.
        modelBuilder.Entity<SettingEntity>().HasData(
            new SettingEntity
            {
                Id = 1,
                Key = "root",
                Value = "root",
                Description = "超级管理员"
            });
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