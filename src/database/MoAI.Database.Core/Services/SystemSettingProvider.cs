// <copyright file="SystemSettingKeys.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Maomi;
using Microsoft.EntityFrameworkCore;
using MoAI.Infra.Extensions;

namespace MoAI.Database.Models;

/// <summary>
/// 系统设置键值列表.
/// </summary>
[InjectOnScoped]
public class SystemSettingProvider : ISystemSettingProvider
{
    //    /// <summary>
    //    /// 超级管理员.
    //    /// </summary>
    //    public static readonly SystemSettingKey Root = new SystemSettingKey
    //    {
    //        Key = "root",
    //        Value = "1",
    //        Description = "超级管理员"
    //    };

    //    /// <summary>
    //    /// 最大上传文件大小，单位为字节，默认 100MB.
    //    /// </summary>
    //    public static readonly SystemSettingKey MaxUploadFileSize = new SystemSettingKey
    //    {
    //        Key = "max_upload_file_size",
    //        Value = (1024 * 1024 * 100).ToJsonString(),
    //        Description = "超级管理员"
    //    };

    //    /// <summary>
    //    /// 最大上传文件大小，单位为字节，默认 100MB.
    //    /// </summary>
    //    public static readonly SystemSettingKey DisableRegister = new SystemSettingKey
    //    {
    //        Key = "disable_register",
    //        Value = false.ToJsonString(),
    //        Description = "超级管理员"
    //    };

    //    /// <summary>
    //    /// AI 对话中用户最大聊天记录数量.
    //    /// </summary>
    //    public static readonly SystemSettingKey UserMaxAIHistoryCount = new SystemSettingKey
    //    {
    //        Key = "user_max_ai_history_count",
    //        Value = 10.ToJsonString(),
    //        Description = "超级管理员"
    //    };

    //    /// <summary>
    //    /// 配置列表.
    //    /// </summary>
    //    public static readonly IReadOnlyCollection<SystemSettingKey> Keys = new List<SystemSettingKey>
    //        {
    //            Root,
    //            MaxUploadFileSize,
    //            DisableRegister,
    //            UserMaxAIHistoryCount
    //        };

    private readonly Lazy<Task<IReadOnlyCollection<SystemSettingKey>>> _lazy;
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemSettingProvider"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public SystemSettingProvider(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;

        _lazy = new Lazy<Task<IReadOnlyCollection<SystemSettingKey>>>(GetSettingsAsync);
    }

    public async Task<string?> GetValueAsync(string key)
    {
        return (await _lazy.Value).FirstOrDefault(x => x.Key == key).Value;
    }

    private async Task<IReadOnlyCollection<SystemSettingKey>> GetSettingsAsync()
    {
        List<SystemSettingKey> settings = new();

        var data = await _databaseContext.Settings.Select(x => new SystemSettingKey
        {
            Key = x.Key,
            Value = x.Value,
            Description = x.Description ?? string.Empty,
        }).ToArrayAsync();

        foreach (var item in ISystemSettingProvider.Keys)
        {
            var result = data.FirstOrDefault(x => x.Key == item.Key);
            if (result != default)
            {
                settings.Add(result);
            }
            else
            {
                settings.Add(item);
            }
        }

        return settings;
    }
}
