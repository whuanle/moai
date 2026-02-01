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
                IsAdmin = true,
                AvatarPath = string.Empty
            });

        // 优化 ClassifyEntity 种子数据插入
        var classifyNames = new[]
        {
            "职业", "商业", "工具", "语言", "办公", "通用", "写作", "精选", "编程", "情感", "教育",
            "创意", "学术", "设计", "艺术", "娱乐", "生活", "医疗", "游戏", "翻译", "音乐", "点评",
            "文案", "百科", "健康", "营销", "科学", "分析", "法律", "咨询", "金融", "旅游", "管理"
        };
        var classifyTypes = new[] { "prompt", "plugin", "app" };
        var classifyEntities = new List<ClassifyEntity>();

        int classifyId = 1;
        foreach (var type in classifyTypes)
        {
            foreach (var name in classifyNames)
            {
                classifyEntities.Add(new ClassifyEntity { Id = classifyId++, Type = type, Name = name, Description = name });
            }
        }

        modelBuilder.Entity<ClassifyEntity>().HasData(classifyEntities);

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