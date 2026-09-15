using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using MoAI.Database.Entities;

namespace MoAI.Database.Seed;

/// <summary>
/// 分类种子数据.
/// </summary>
public static class ClassifySeed
{
    /// <summary>
    /// 应用分类种子数据.
    /// </summary>
    /// <param name="modelBuilder">模型构建器.</param>
    public static void Apply(ModelBuilder modelBuilder)
    {
        var classifyNames = new[]
        {
            "职业", "商业", "工具", "语言", "办公", "通用", "写作", "精选", "编程",
            "创意", "学术", "设计", "翻译",
            "分析", "法律", "咨询", "金融", "管理"
        };
        var classifyTypes = new[] { "plugin", "app", "kb", "prompt" };
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
    }
}
