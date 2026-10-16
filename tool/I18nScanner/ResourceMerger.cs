using System.Collections.ObjectModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace I18nScanner;

/// <summary>
/// i18n JSON 资源文件的加载、合并与写出.
/// </summary>
public static class ResourceMerger
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    /// <summary>
    /// 读取扁平 key/value JSON；文件不存在时返回空表.
    /// </summary>
    /// <param name="path">JSON 文件路径.</param>
    /// <param name="invalidEntries">值不是字符串的条目（被丢弃）.</param>
    /// <returns>有序字典（Ordinal 排序）.</returns>
    public static SortedDictionary<string, string> LoadFlat(string path, Collection<string>? invalidEntries = null)
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(path))
        {
            return result;
        }

        using var document = JsonDocument.Parse(
            File.ReadAllText(path),
            new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String)
            {
                result[property.Name] = property.Value.GetString()!;
            }
            else
            {
                invalidEntries?.Add(property.Name);
            }
        }

        return result;
    }

    /// <summary>
    /// 以扫描出的 key 集合合并现有资源：已有翻译保留、新 key 以 key 自身兜底、消失的 key 剪除.
    /// </summary>
    /// <param name="existing">现有资源表（原地更新为合并结果）.</param>
    /// <param name="keys">扫描出的全部 key.</param>
    /// <returns>合并结果.</returns>
    public static ResourceMergeResult Merge(SortedDictionary<string, string> existing, IEnumerable<string> keys)
    {
        var result = new ResourceMergeResult();
        var next = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var key in keys)
        {
            if (existing.TryGetValue(key, out var value))
            {
                next[key] = value;
                result.Kept++;
            }
            else
            {
                next[key] = key;
                result.Added.Add(key);
            }
        }

        foreach (var key in existing.Keys)
        {
            if (!next.ContainsKey(key))
            {
                result.Removed.Add(key);
            }
        }

        existing.Clear();
        foreach (var pair in next)
        {
            existing[pair.Key] = pair.Value;
        }

        return result;
    }

    /// <summary>
    /// 写出扁平 key/value JSON（缩进、不转义中文、UTF-8 无 BOM）.
    /// </summary>
    /// <param name="path">目标文件路径.</param>
    /// <param name="resources">资源表.</param>
    public static void WriteFlat(string path, SortedDictionary<string, string> resources)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new Utf8JsonWriter(stream, WriterOptions);
        writer.WriteStartObject();
        foreach (var pair in resources)
        {
            writer.WriteString(pair.Key, pair.Value);
        }

        writer.WriteEndObject();
        writer.Flush();

        // 末尾补换行，保持 git diff 干净
        stream.WriteByte((byte)'\n');
    }
}
