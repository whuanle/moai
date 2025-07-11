// <copyright file="FileUploadHelper.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using System.IO.Pipelines;

namespace MoAI.Storage.Helper;

/// <summary>
/// FileUploadHelper.
/// </summary>
public static class FileUploadHelper
{
    // 文件分片
    private const int BufferSize = 16 * 1024 * 1024; // 16 MB buffer size

    /// <summary>
    /// 保存流到文件中.
    /// </summary>
    /// <param name="targetFilePath"></param>
    /// <param name="length"></param>
    /// <param name="contentReader"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static async Task SaveViaPipeReaderAsync(string targetFilePath, int length, PipeReader contentReader, CancellationToken cancellationToken)
    {
        var dir = Directory.GetParent(targetFilePath)!;
        if (!dir.Exists)
        {
            dir.Create();
        }

        long totalBytesRead = 0;

        using FileStream outputFileStream = new FileStream(
            path: targetFilePath,
            mode: FileMode.CreateNew,
            access: FileAccess.Write,
            share: FileShare.None,
            bufferSize: BufferSize,
            useAsync: true);

        while (true)
        {
            var readResult = await contentReader.ReadAsync();
            var buffer = readResult.Buffer;

            foreach (var memory in buffer)
            {
                await outputFileStream.WriteAsync(memory);
                totalBytesRead += memory.Length;
            }

            contentReader.AdvanceTo(buffer.End);

            if (readResult.IsCompleted)
            {
                break;
            }
        }
    }
}
