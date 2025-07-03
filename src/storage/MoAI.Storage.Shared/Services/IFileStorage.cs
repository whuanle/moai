// <copyright file="IFileStorage.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

namespace MoAI.Store.Services;

/// <summary>
/// 文件存储接口.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// 上传流并指定文件名称.
    /// </summary>
    /// <param name="inputStream"></param>
    /// <param name="objectKey"></param>
    /// <returns>Task.</returns>
    Task UploadFileAsync(Stream inputStream, string objectKey);

    /// <summary>
    /// 判断文件是否存在.
    /// </summary>
    /// <param name="objectKey"></param>
    /// <returns>是否存在.</returns>
    Task<bool> FileExistsAsync(string objectKey);

    /// <summary>
    /// 判断文件是否存在.
    /// </summary>
    /// <param name="objectKeys"></param>
    /// <returns>是否存在.</returns>
    Task<IReadOnlyDictionary<string, bool>> FilesExistsAsync(IEnumerable<string> objectKeys);

    /// <summary>
    /// 获取文件大小.
    /// </summary>
    /// <param name="objectKey"></param>
    /// <returns>文件信息.</returns>
    Task<long> GetFileSizeAsync(string objectKey);

    /// <summary>
    /// 批量获取文件大小.
    /// </summary>
    /// <param name="objectKeys"></param>
    /// <returns>文件信息.</returns>
    Task<IReadOnlyDictionary<string, long>> GetFileSizeAsync(IEnumerable<string> objectKeys);

    /// <summary>
    /// 批量删除文件.
    /// </summary>
    /// <param name="objectKeys"></param>
    /// <returns>Task.</returns>
    Task DeleteFilesAsync(IEnumerable<string> objectKeys);

    /// <summary>
    /// 生成预签名上传地址.
    /// </summary>
    /// <param name="fileObject"></param>
    /// <returns>预签名地址.</returns>
    Task<string> GeneratePreSignedUploadUrlAsync(FileObject fileObject);

    /// <summary>
    /// 下载文件到本地.
    /// </summary>
    /// <param name="objectKey"></param>
    /// <param name="filePath"></param>
    /// <returns></returns>
    Task DownloadAsync(string objectKey, string filePath);
}
