using System;
using System.Collections.Generic;
using System.Linq;

namespace MoAI.Database.Seed;

/// <summary>
/// 内置设置项注册表，作为种子数据与首次写入时的初始数据来源.
/// </summary>
public static class SettingDefinitions
{
    /// <summary>
    /// 允许第三方账号登录直接创建账号.
    /// </summary>
    public const string OAuthAutoRegisterKey = "oauth_auto_register";

    private static readonly List<SettingDefinition> BackingField = new()
    {
        new SettingDefinition
        {
            Key = OAuthAutoRegisterKey,
            Name = "允许第三方账号登录直接创建账号",
            Description = "开启后，第三方授权登录（例如 GitHub、Google 等）在未注册时将自动创建账号.",
            DefaultValue = "false"
        }
    };

    /// <summary>
    /// 全部内置设置项.
    /// </summary>
    public static IReadOnlyList<SettingDefinition> All => BackingField;

    /// <summary>
    /// 根据 key 查找设置项，未找到返回 null.
    /// </summary>
    /// <param name="key">设置项 key.</param>
    /// <returns>返回 <see cref="SettingDefinition"/> 或 null.</returns>
    public static SettingDefinition? Find(string key)
        => BackingField.FirstOrDefault(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
}
