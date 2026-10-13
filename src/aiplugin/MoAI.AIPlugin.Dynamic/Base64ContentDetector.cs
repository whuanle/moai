using System.Text;

namespace MoAI.AIPlugin.Dynamic;

/// <summary>
/// Base64 解码内容的类型嗅探：数据 URL 前缀剥离、图片魔数识别、UTF-8 文本判定.
/// </summary>
public static class Base64ContentDetector
{
    /// <summary>
    /// 尝试剥离 data URL 前缀（形如 <c>data:image/png;base64,xxxx</c>）.
    /// </summary>
    /// <param name="input">原始输入.</param>
    /// <param name="mimeType">前缀中声明的 MIME 类型（可能为空串）.</param>
    /// <param name="base64Payload">去掉前缀后的 Base64 载荷（可能仍含空白字符）.</param>
    /// <returns>是 data URL 形式返回 true.</returns>
    public static bool TryStripDataUrlPrefix(string input, out string mimeType, out string base64Payload)
    {
        mimeType = string.Empty;
        base64Payload = string.Empty;

        var trimmed = input.Trim();
        if (!trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var separator = trimmed.IndexOf(";base64,", StringComparison.OrdinalIgnoreCase);
        if (separator < 0)
        {
            return false;
        }

        mimeType = trimmed[5..separator].Trim();
        base64Payload = trimmed[(separator + ";base64,".Length)..];
        return true;
    }

    /// <summary>
    /// 按魔数识别常见图片格式.
    /// </summary>
    /// <param name="bytes">解码后的字节.</param>
    /// <param name="contentType">识别到的 MIME 类型.</param>
    /// <param name="extension">对应扩展名（含点）.</param>
    /// <returns>识别为图片返回 true.</returns>
    public static bool IsImageData(ReadOnlySpan<byte> bytes, out string contentType, out string extension)
    {
        contentType = string.Empty;
        extension = string.Empty;

        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes.Slice(1, 3).SequenceEqual("PNG"u8))
        {
            contentType = "image/png";
            extension = ".png";
            return true;
        }

        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            contentType = "image/jpeg";
            extension = ".jpg";
            return true;
        }

        if (bytes.Length >= 6 && (bytes.Slice(0, 6).SequenceEqual("GIF87a"u8) || bytes.Slice(0, 6).SequenceEqual("GIF89a"u8)))
        {
            contentType = "image/gif";
            extension = ".gif";
            return true;
        }

        if (bytes.Length >= 12 && bytes.Slice(0, 4).SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            contentType = "image/webp";
            extension = ".webp";
            return true;
        }

        if (bytes.Length >= 2 && bytes[0] == (byte)'B' && bytes[1] == (byte)'M')
        {
            contentType = "image/bmp";
            extension = ".bmp";
            return true;
        }

        if (bytes.Length >= 4 && bytes[0] == 0x00 && bytes[1] == 0x00 && bytes[2] == 0x01 && bytes[3] == 0x00)
        {
            contentType = "image/x-icon";
            extension = ".ico";
            return true;
        }

        if (bytes.Length >= 4
            && ((bytes[0] == (byte)'I' && bytes[1] == (byte)'I' && bytes[2] == 0x2A && bytes[3] == 0x00)
                || (bytes[0] == (byte)'M' && bytes[1] == (byte)'M' && bytes[2] == 0x00 && bytes[3] == 0x2A)))
        {
            contentType = "image/tiff";
            extension = ".tiff";
            return true;
        }

        return false;
    }

    /// <summary>
    /// 判定文本是否为 SVG（SVG 是文本，但应按图片处理才能在对话里渲染）.
    /// </summary>
    /// <param name="text">解码后的文本.</param>
    /// <returns>形似 SVG 文档返回 true.</returns>
    public static bool IsSvgText(string text)
    {
        var trimmed = text.TrimStart();
        if (trimmed.StartsWith("<svg", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)
            && trimmed.Contains("<svg", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 严格 UTF-8 解码判定是否为文本：解码失败或含 NUL 等二进制特征控制字符时视为非文本.
    /// </summary>
    /// <param name="bytes">解码后的字节.</param>
    /// <param name="text">解码成功时的文本.</param>
    /// <returns>是 UTF-8 文本返回 true.</returns>
    public static bool IsUtf8Text(ReadOnlySpan<byte> bytes, out string text)
    {
        text = string.Empty;
        if (bytes.Length == 0)
        {
            return false;
        }

        string decoded;
        try
        {
            decoded = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return false;
        }

        foreach (var c in decoded)
        {
            // 允许 \t \n \v \f \r；其余 C0 控制字符（NUL、SOH、DLE 等）是二进制特征.
            if (c < '\u0020' && c is not ('\t' or '\n' or '\u000B' or '\u000C' or '\r'))
            {
                return false;
            }
        }

        text = decoded;
        return true;
    }
}
