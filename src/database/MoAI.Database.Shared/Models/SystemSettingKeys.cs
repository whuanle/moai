// <copyright file="SystemSettingKeys.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using System.ComponentModel;

namespace MoAI.Database.Models;

/// <summary>
/// 系统设置键值列表.
/// </summary>
public static class SystemSettingKeys
{
    /// <summary>
    /// 超级管理员.
    /// </summary>
    [Description("超级管理员")]
    public const string Root = "root";

    /// <summary>
    /// 超级管理员.
    /// </summary>
    public const int RootValue = 1;

    /// <summary>
    /// 最大上传文件大小，单位为字节，默认 100MB.
    /// </summary>
    [Description("最大上传文件大小，单位为字节")]
    public const string MaxUploadFileSize = "max_upload_file_size";

    public const int MaxUploadFileSizeValue = 1024 * 1024 * 100;

    /// <summary>
    /// 是否禁用注册功能，默认不禁用注册功能.
    /// </summary>
    [Description("是否禁用注册功能")]
    public const string DisableRegister = "disable_register";
    public const bool DisableRegisterValue = false;

    private static readonly IReadOnlyDictionary<string, string> _defaultValues;

    static SystemSettingKeys()
    {
        var defaultValues = new Dictionary<string, string>();

        // 生成系统初始化配置.
        var fields = typeof(SystemSettingKeys).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        var fieldDictionary = fields.ToDictionary(x => x.Name, x => x);
        foreach (var field in fields
            .Where(x => !x.Name.EndsWith("Value", StringComparison.CurrentCultureIgnoreCase)))
        {
            var valueField = fieldDictionary.GetValueOrDefault($"{field.Name}Value");
            if (valueField == null)
            {
                continue;
            }

            var key = field.GetValue(null)?.ToString() ?? string.Empty;
            var value = valueField.GetValue(null)?.ToString() ?? string.Empty;

            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
            {
                continue;
            }

            defaultValues[key] = value;
        }

        _defaultValues = defaultValues;
    }

    /// <summary>
    /// 解析配置.
    /// </summary>
    /// <param name="kvs"></param>
    /// <returns></returns>
    public static IReadOnlyDictionary<string, string> ParseSettings(IReadOnlyDictionary<string, string> kvs)
    {
        Dictionary<string, string> settings = new();
        foreach (var kv in _defaultValues)
        {
            if (kvs.TryGetValue(kv.Key, out var value))
            {
                settings[kv.Key] = value;
            }
            else
            {
                settings[kv.Key] = kv.Value;
            }
        }

        return settings;
    }
}
