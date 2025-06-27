// <copyright file="FileObject.cs" company="MaomiAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/AIDotNet/MaomiAI
// </copyright>

namespace MoAI.Store.Services;

public class FileObject
{
    /// <summary>
    /// 文件对象key.
    /// </summary>
    public string ObjectKey { get; set; } = string.Empty;

    /// <summary>
    /// 过期时间.
    /// </summary>
    public TimeSpan ExpiryDuration { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// 可以上传的文件最大限制值，避免用户上传过大的文件.
    /// </summary>
    public int MaxFileSize { get; set; }

    /// <summary>
    /// 限制上传的文件类型.
    /// </summary>
    public string ContentType { get; set; } = string.Empty;
}