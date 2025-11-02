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
            var readResult = await contentReader.ReadAsync(cancellationToken);
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

        if (cancellationToken.IsCancellationRequested)
        {
            // 如果取消了，则删除文件
            if (File.Exists(targetFilePath))
            {
                File.Delete(targetFilePath);
            }

            throw new OperationCanceledException("File upload was canceled.");
        }
    }
}
