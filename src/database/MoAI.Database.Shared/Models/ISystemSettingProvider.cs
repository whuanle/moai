// <copyright file="ISystemSettingProvider.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MoAI.Infra.Extensions;

namespace MoAI.Database.Models;

/// <summary>
/// 系统配置提供器.
/// </summary>
public interface ISystemSettingProvider
{
    /// <summary>
    /// 超级管理员.
    /// </summary>
    public static readonly SystemSettingKey Root = new SystemSettingKey
    {
        Key = "root",
        Value = "1",
        Description = "超级管理员"
    };

    /// <summary>
    /// 最大上传文件大小，单位为字节，默认 100MB.
    /// </summary>
    public static readonly SystemSettingKey SystemName = new SystemSettingKey
    {
        Key = "system_name",
        Value = "MoAI",
        IsEdit = true,
        Description = "系统名字"
    };

    /// <summary>
    /// 最大上传文件大小，单位为字节，默认 100MB.
    /// </summary>
    public static readonly SystemSettingKey MaxUploadFileSize = new SystemSettingKey
    {
        Key = "max_upload_file_size",
        Value = (1024 * 1024 * 100).ToJsonString(),
        IsEdit = true,
        Description = "最大上传文件大小，单位为字节，默认 100MB"
    };

    /// <summary>
    /// 禁止注册.
    /// </summary>
    public static readonly SystemSettingKey DisableRegister = new SystemSettingKey
    {
        Key = "disable_register",
        Value = false.ToJsonString(),
        IsEdit = true,
        Description = "禁用注册功能"
    };

    /// <summary>
    /// 配置列表.
    /// </summary>
    public static readonly IReadOnlyCollection<SystemSettingKey> Keys = new List<SystemSettingKey>
        {
            Root,
            SystemName,
            MaxUploadFileSize,
            DisableRegister
        };

    /// <summary>
    /// 获取指定键的值.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    Task<string> GetValueAsync(string key);
}