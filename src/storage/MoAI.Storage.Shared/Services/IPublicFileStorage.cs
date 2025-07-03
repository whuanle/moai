// <copyright file="IPublicFileStorage.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Store.Services;

public interface IPublicFileStorage : IFileStorage
{
    /// <summary>
    /// 获取文件下载地址.
    /// </summary>
    /// <param name="objectKey"></param>
    /// <returns>文件地址.</returns>
    Task<Uri> GetFileUrlAsync(string objectKey);

    /// <summary>
    /// 批量获取文件地址，返回文件地址字典.
    /// </summary>
    /// <param name="objectKeys"></param>
    /// <returns>文件地址.</returns>
    Task<IReadOnlyDictionary<string, Uri>> GetFileUrlAsync(IEnumerable<string> objectKeys);
}
