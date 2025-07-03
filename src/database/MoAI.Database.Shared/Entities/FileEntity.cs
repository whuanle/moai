// <copyright file="FileEntity.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MoAI.Database.Audits;

namespace MoAI.Database.Entities;

/// <summary>
/// 文件列表.
/// </summary>
public partial class FileEntity : IFullAudited
{
    /// <summary>
    /// id.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 文件名称.
    /// </summary>
    public string FileName { get; set; } = default!;

    /// <summary>
    /// 文件路径.
    /// </summary>
    public string ObjectKey { get; set; } = default!;

    /// <summary>
    /// md5.
    /// </summary>
    public string FileMd5 { get; set; } = default!;

    /// <summary>
    /// 文件大小.
    /// </summary>
    public int FileSize { get; set; }

    /// <summary>
    /// 文件类型.
    /// </summary>
    public string ContentType { get; set; } = default!;

    public bool IsPublic { get; set; }

    public bool IsUploaded { get; set; }

    /// <summary>
    /// 创建人.
    /// </summary>
    public int CreateUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTimeOffset CreateTime { get; set; }

    /// <summary>
    /// 最后修改人.
    /// </summary>
    public int UpdateUserId { get; set; }

    /// <summary>
    /// 最后更新时间.
    /// </summary>
    public DateTimeOffset UpdateTime { get; set; }

    /// <summary>
    /// 软删除.
    /// </summary>
    public long IsDeleted { get; set; }
}
