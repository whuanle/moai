using System.Buffers;

namespace MoAI.Storage.Helpers;

/// <summary>
/// 文件流扩展.
/// </summary>
public static class StreamExtensions
{
    /// <summary>
    /// 将 Stream 转换为 ReadOnlyMemory{byte}，尽可能避免内存复制.
    /// </summary>
    /// <param name="stream">待转换的流</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns></returns>
    public static async ValueTask<ReadOnlyMemory<byte>> ToReadOnlyMemoryAsync(this Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead)
        {
            throw new NotSupportedException("流不支持读取操作");
        }

        if (stream is MemoryStream ms)
        {
            if (ms.TryGetBuffer(out ArraySegment<byte> buffer))
            {
                return new ReadOnlyMemory<byte>(buffer.Array, buffer.Offset, (int)ms.Length);
            }

            return ms.ToArray();
        }

        int initialLength = stream.CanSeek ? (int)stream.Length : 4096;
        byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(initialLength);
        int bytesRead = 0;

        try
        {
            int read;
            while ((read = await stream.ReadAsync(
                       rentedBuffer.AsMemory(bytesRead), cancellationToken)) > 0)
            {
                bytesRead += read;
                if (bytesRead == rentedBuffer.Length)
                {
                    byte[] newBuffer = ArrayPool<byte>.Shared.Rent(rentedBuffer.Length * 2);
                    Array.Copy(rentedBuffer, newBuffer, bytesRead);
                    ArrayPool<byte>.Shared.Return(rentedBuffer);
                    rentedBuffer = newBuffer;
                }
            }

            return new ReadOnlyMemory<byte>(rentedBuffer, 0, bytesRead);
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer);
            throw;
        }
    }

    /// <summary>
    /// 转换流.
    /// </summary>
    /// <param name="stream"></param>
    /// <returns></returns>
    public static ReadOnlyMemory<byte> ToReadOnlyMemory(this Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
        {
            throw new NotSupportedException("流不支持读取操作");
        }

        if (stream is MemoryStream ms)
        {
            if (ms.TryGetBuffer(out ArraySegment<byte> buffer))
            {
                return new ReadOnlyMemory<byte>(buffer.Array, buffer.Offset, (int)ms.Length);
            }

            return ms.ToArray();
        }

        int initialLength = stream.CanSeek ? (int)stream.Length : 4096;
        byte[] rentedBuffer = ArrayPool<byte>.Shared.Rent(initialLength);
        int bytesRead = 0;

        try
        {
            int read;
            while ((read = stream.Read(rentedBuffer, bytesRead, rentedBuffer.Length - bytesRead)) > 0)
            {
                bytesRead += read;
                if (bytesRead == rentedBuffer.Length)
                {
                    byte[] newBuffer = ArrayPool<byte>.Shared.Rent(rentedBuffer.Length * 2);
                    Array.Copy(rentedBuffer, newBuffer, bytesRead);
                    ArrayPool<byte>.Shared.Return(rentedBuffer);
                    rentedBuffer = newBuffer;
                }
            }

            return new ReadOnlyMemory<byte>(rentedBuffer, 0, bytesRead);
        }
        catch
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer);
            throw;
        }
    }
}