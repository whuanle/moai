using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using MoAI.Database.Entities;

namespace MoAI.Database.Seed;

/// <summary>
/// 设置种子数据.
/// </summary>
public static class SettingSeed
{
    /// <summary>
    /// 应用设置种子数据.
    /// </summary>
    /// <param name="modelBuilder">模型构建器.</param>
    public static void Apply(ModelBuilder modelBuilder)
    {
        var settings = new List<SettingEntity>
        {
            new SettingEntity
            {
                Id = 1,
                Key = "root",
                Value = "1",
                Description = "超级管理员"
            }
        };

        int settingId = 2;
        foreach (var definition in SettingDefinitions.All)
        {
            settings.Add(new SettingEntity
            {
                Id = settingId++,
                Key = definition.Key,
                Name = definition.Name,
                Description = definition.Description,
                Value = definition.DefaultValue
            });
        }

        modelBuilder.Entity<SettingEntity>().HasData(settings);
    }
}
