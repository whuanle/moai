using System.Buffers;
using System.Text;

namespace MoAI.Gateway.Protocols;

/// <summary>
/// SSE 帧读取器：把上游 SSE 字节流按帧拆为 data 载荷.
/// </summary>
public static class SseFrameReader
{
    /// <summary>
    /// 逐帧读取 SSE 流.
    /// </summary>
    /// <param name="stream">响应流.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回每帧的 data 载荷（多行 data 以 \n 连接）.</returns>
    public static async IAsyncEnumerable<string> ReadAsync(Stream stream, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var buffer = new byte[8192];
        var sb = new StringBuilder();
        var dataLines = new List<string>();
        var line = new StringBuilder();

        while (!cancellationToken.IsCancellationRequested)
        {
            var read = await stream.ReadAsync(new Memory<byte>(buffer), cancellationToken);
            if (read == 0)
            {
                break;
            }

            var chunk = Encoding.UTF8.GetString(buffer, 0, read);
            foreach (var ch in chunk)
            {
                if (ch == '\n')
                {
                    var text = line.ToString();
                    line.Clear();

                    if (text.Length == 0 || text == "\r")
                    {
                        // 空行 = 帧结束.
                        if (dataLines.Count > 0)
                        {
                            yield return string.Join("\n", dataLines);
                            dataLines.Clear();
                        }
                    }
                    else
                    {
                        if (text.EndsWith("\r", StringComparison.Ordinal))
                        {
                            text = text[..^1];
                        }

                        if (text.StartsWith("data:", StringComparison.Ordinal))
                        {
                            var payload = text.Substring("data:".Length);
                            dataLines.Add(payload.StartsWith(' ') ? payload.Substring(1) : payload);
                        }

                        // event:/id:/retry:/comment 行忽略：事件类型信息在 JSON 的 type 字段中.
                    }
                }
                else
                {
                    line.Append(ch);
                }
            }
        }

        if (dataLines.Count > 0)
        {
            yield return string.Join("\n", dataLines);
        }
    }
}
