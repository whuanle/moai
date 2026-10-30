using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MoAI.Wiki.Services;

/// <summary>
/// 纯文本文件读取兜底：Maomi.ToMarkdown 不识别的文本类扩展名（管理员在允许类型中放开的 .csv/.sh/.cs 等）按文本读取.
/// 二进制文件明确拒绝，避免产出乱码文本进入切割与向量化.
/// </summary>
public static class WikiTextFileReader
{
    /// <summary>
    /// 二进制嗅探窗口大小（NUL 字节判定）.
    /// </summary>
    private const int SniffLength = 8192;

    /// <summary>
    /// 将流按纯文本读取（自动识别 BOM，默认 UTF-8）；起始窗口内含 NUL 字节视为二进制文件并抛出异常.
    /// 源流不要求可 Seek（如对象存储响应流），整体拷贝到内存后处理.
    /// </summary>
    /// <param name="stream">文件流.</param>
    /// <param name="fileName">文件名，用于错误提示与日志.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>文本内容.</returns>
    public static async Task<string> ReadAsTextAsync(Stream stream, string fileName, CancellationToken cancellationToken)
    {
        if (stream.CanSeek)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }

        using var buffered = new MemoryStream();
        await stream.CopyToAsync(buffered, cancellationToken);

        var bytes = buffered.GetBuffer();
        var sniffLength = (int)Math.Min(buffered.Length, SniffLength);
        for (var i = 0; i < sniffLength; i++)
        {
            if (bytes[i] == 0)
            {
                throw new InvalidOperationException($"文件为二进制格式，无法提取文本内容: {fileName}");
            }
        }

        using var reader = new StreamReader(new MemoryStream(bytes, 0, (int)buffered.Length), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
