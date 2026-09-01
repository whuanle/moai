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
    }
}
