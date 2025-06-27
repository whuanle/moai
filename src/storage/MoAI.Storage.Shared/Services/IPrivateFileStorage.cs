// <copyright file="IPrivateFileStore.cs" company="MaomiAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/AIDotNet/MaomiAI
// </copyright>

namespace MoAI.Store.Services;

public interface IPrivateFileStorage : IFileStorage
{
    /// <summary>
    /// 获取文件下载地址并设置有效时间.
    /// </summary>
    /// <param name="objectKey"></param>
    /// <param name="expiryDuration"></param>
    /// <returns>文件地址.</returns>
    Task<Uri> GetFileUrlAsync(string objectKey, TimeSpan expiryDuration);

    /// <summary>
    /// 批量获取文件地址并设置有效时间.
    /// </summary>
    /// <param name="objectKeys"></param>
    /// <param name="expiryDuration"></param>
    /// <returns>文件地址.</returns>
    Task<IReadOnlyDictionary<string, Uri>> GetFileUrlAsync(IEnumerable<string> objectKeys, TimeSpan expiryDuration);
}
