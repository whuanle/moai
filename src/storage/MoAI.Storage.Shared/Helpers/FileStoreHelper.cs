#pragma warning disable CA1054 // 类 URI 参数不应为字符串
using MimeKit;
using System.Security.Cryptography;

namespace MoAI.Storage.Helpers;

/// <summary>
/// 文件存储助手类.
/// </summary>
public static class FileStoreHelper
{
    /// <summary>
    /// 公开访问目录前缀.
    /// <para>
    /// 允许通过 web 服务直接中转访问（免登录、静态地址，经 /static 中间件）的文件，
    /// 必须存储在该目录下。ObjectKey 约定为 <c>public/...</c>.
    /// </para>
    /// </summary>
    public const string PublicPrefix = "public";

    /// <summary>
    /// 常用的图片扩展名.
    /// </summary>
    public static readonly IReadOnlyCollection<string> ImageExtensions = new string[]
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".gif",
        ".bmp",
        ".webp",
        ".svg",
        ".tiff",
        ".ico"
    };

    /// <summary>
    /// 常用的文档格式.
    /// </summary>
    public static readonly IReadOnlyCollection<string> DocumentFormats = new string[]
    {
        ".md",     // Markdown
        ".pdf",    // Portable Document Format
        ".doc",    // Microsoft Word 97-2003
        ".docx",   // Microsoft Word
        ".xls",    // Microsoft Excel 97-2003
        ".xlsx",   // Microsoft Excel
        ".ppt",    // Microsoft PowerPoint 97-2003
        ".pptx",   // Microsoft PowerPoint
        ".txt",    // Plain Text
        ".rtf",    // Rich Text Format
        ".odt",    // OpenDocument Text
        ".ods",    // OpenDocument Spreadsheet
        ".odp",    // OpenDocument Presentation
        ".csv",    // Comma-Separated Values
        ".json",   // JSON (JavaScript Object Notation)
        ".xml",    // XML (eXtensible Markup Language)
        ".html",   // HTML (HyperText Markup Language)
        ".htm",    // HTML (HyperText Markup Language)
        ".epub",   // EPUB (Electronic Publication)
        ".mobi",   // MOBI (Mobipocket)
        ".ps",     // PostScript
        ".tex",    // LaTeX Source Document
        ".dvi",    // Device Independent File Format (LaTeX)
        ".djvu",   // DjVu
        ".msg",    // Microsoft Outlook Email Message
        ".eml",    // EML (Email Message)
        ".xps",    // XML Paper Specification
        ".gdoc",   // Google Docs
        ".gsheet", // Google Sheets
        ".gslides" // Google Slides
    };

    /// <summary>
    /// 生成文件 ObjectKey.
    /// </summary>
    /// <param name="sha256">文件 sha256.</param>
    /// <param name="fileName">文件名称.</param>
    /// <param name="prefix">前缀目录.</param>
    /// <returns>ObjectKey.</returns>
    public static string GetObjectKey(string sha256, string fileName, string? prefix = "")
    {
        var fileExtensions = Path.GetExtension(fileName);
        fileExtensions = fileExtensions.TrimStart('.');

        var objectKey = $"{sha256}.{fileExtensions}";
        if (!string.IsNullOrEmpty(prefix))
        {
            return $"{prefix}/{objectKey}";
        }

        return objectKey;
    }

    /// <summary>
    /// 为 ObjectKey 拼接公开访问目录前缀（固定规则：公开文件存储在 public 目录下）.
    /// </summary>
    /// <param name="objectKey">对象 key.</param>
    /// <returns>添加 public 前缀后的对象 key.</returns>
    public static string ToPublicObjectKey(string objectKey)
    {
        var key = objectKey.TrimStart('/');
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("ObjectKey 不能为空.", nameof(objectKey));
        }

        if (IsPublicObjectKey(key))
        {
            return key;
        }

        return $"{PublicPrefix}/{key}";
    }

    /// <summary>
    /// 判断 ObjectKey 是否允许公开访问（即是否位于 public 目录下）.
    /// </summary>
    /// <param name="objectKey">对象 key.</param>
    /// <returns>是否允许公开访问.</returns>
    public static bool IsPublicObjectKey(string objectKey)
    {
        var key = objectKey.TrimStart('/');
        return key.StartsWith($"{PublicPrefix}/", StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, PublicPrefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 计算文件的 SHA-256 哈希值.
    /// </summary>
    /// <param name="stream">文件流.</param>
    /// <returns>文件 SHA-256.</returns>
    public static string CalculateFileSha256(Stream stream)
    {
        var hash = SHA256.HashData(stream);
        stream.Seek(0, SeekOrigin.Begin);
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// 获取文件的 MIME 类型.
    /// </summary>
    /// <param name="filePath">文件路径.</param>
    /// <returns>MIME 类型.</returns>
    public static string GetMimeType(string filePath)
    {
        return MimeTypes.GetMimeType(filePath);
    }

    /// <summary>
    /// 获取文件的 MIME 类型.
    /// </summary>
    /// <param name="extension">文件扩展名（含点号）.</param>
    /// <param name="fallback">无法识别时的兜底类型.</param>
    /// <returns>MIME 类型.</returns>
    public static string GetMimeType(string extension, string fallback)
    {
        if (string.IsNullOrEmpty(extension))
        {
            return fallback;
        }

        var mime = MimeTypes.GetMimeType(extension);
        if (string.IsNullOrEmpty(mime) || mime == "application/octet-stream")
        {
            return fallback;
        }

        return mime;
    }
}
