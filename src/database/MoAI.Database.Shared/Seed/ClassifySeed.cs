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
    /// 分类名称与表情的对应关系（同一名称跨类型共用同一表情）.
    /// </summary>
    private static readonly Dictionary<string, string> ClassifyEmojis = new()
    {
        ["职业"] = "💼",
        ["工具"] = "🔧",
        ["办公"] = "🏢",
        ["通用"] = "🧩",
        ["写作"] = "✍️",
        ["精选"] = "⭐",
        ["编程"] = "💻",
        ["创意"] = "💡",
        ["设计"] = "🎨",
        ["翻译"] = "🌍",
        ["文案"] = "📄",
        ["营销"] = "📣",
        ["科学"] = "🔬",
        ["分析"] = "📊",
        ["法律"] = "⚖️",
        ["金融"] = "🏦",
    };

    /// <summary>
    /// 应用分类种子数据.
    /// </summary>
    /// <param name="modelBuilder">模型构建器.</param>
    public static void Apply(ModelBuilder modelBuilder)
    {
        var classifyTypes = new[] { "plugin", "app", "kb", "prompt" };
        var classifyEntities = new List<ClassifyEntity>();

        int classifyId = 1;
        foreach (var type in classifyTypes)
        {
            foreach (var name in ClassifyEmojis)
            {
                classifyEntities.Add(new ClassifyEntity { Id = classifyId++, Type = type, Name = name.Key, Description = name.Key, Emoji = name.Value });
            }
        }

        modelBuilder.Entity<ClassifyEntity>().HasData(classifyEntities);
    }
}
