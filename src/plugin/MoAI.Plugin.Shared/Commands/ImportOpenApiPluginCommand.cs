// <copyright file="ImportOpenApiPluginCommand.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Plugin.Commands;

/// <summary>
/// 导入 openapi 文件，支持 json、yaml.
/// </summary>
public class ImportOpenApiPluginCommand : IRequest<SimpleInt>
{
    /// <summary>
    /// 上传的 id.
    /// </summary>
    public int FileId { get; init; }

    /// <summary>
    /// 文件名称.
    /// </summary>
    public string FileName { get; init; } = default!;

    /// <summary>
    /// 插件名称.
    /// </summary>
    public string Name { get; init; } = default!;

    /// <summary>
    /// 插件标题，可中文.
    /// </summary>
    public string Title { get; init; } = default!;

    /// <summary>
    /// 描述.
    /// </summary>
    public string Description { get; init; } = default!;

    /// <summary>
    /// 是否公开.
    /// </summary>
    public bool IsPublic { get; init; } = default!;
}
