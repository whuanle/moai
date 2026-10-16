using System.Collections.ObjectModel;
using System.Text;
using I18nScanner;

Console.OutputEncoding = Encoding.UTF8;

var options = ScanOptions.Parse(args);
if (options == null)
{
    ScanOptions.PrintUsage();
    return 2;
}

if (!Directory.Exists(options.SourceDirectory))
{
    Console.Error.WriteLine($"源码目录不存在: {options.SourceDirectory}");
    return 1;
}

var files = EnumerateSourceFiles(options.SourceDirectory).ToList();
Console.WriteLine($"扫描 {files.Count} 个源文件（{options.SourceDirectory}）...");

var entries = BusinessMessageScanner.Scan(files, options.SourceDirectory);

var literalEntries = entries.Where(e => e.Kind == MessageExpressionKind.Literal).ToList();
var interpolatedEntries = entries.Where(e => e.Kind == MessageExpressionKind.Interpolated).ToList();
var unresolvedEntries = entries.Where(e => e.Kind == MessageExpressionKind.Unresolved).ToList();
var allKeys = literalEntries.Concat(interpolatedEntries)
    .Select(e => e.Template!)
    .Distinct(StringComparer.Ordinal)
    .ToList();

Console.WriteLine($"BusinessException 构造点 {entries.Count} 处：字面量 key {literalEntries.Select(e => e.Template).Distinct().Count()} 个，插值模板 {interpolatedEntries.Select(e => e.Template).Distinct().Count()} 个，未解析 {unresolvedEntries.Count} 处。");

// 两个语言文件走同一套合并语义：已有翻译保留、新 key 以 key 自身兜底、消失的 key 剪除
var zhPath = Path.Combine(options.OutputDirectory, "zh-CN.json");
var enPath = Path.Combine(options.OutputDirectory, "en-US.json");

var zhInvalid = new Collection<string>();
var enInvalid = new Collection<string>();
var zh = ResourceMerger.LoadFlat(zhPath, zhInvalid);
var en = ResourceMerger.LoadFlat(enPath, enInvalid);

var enResult = ResourceMerger.Merge(en, allKeys);

// zh-CN 以原文兜底；若有人调整过 zh-CN 的值也一并保留
foreach (var key in allKeys)
{
    if (!zh.ContainsKey(key))
    {
        zh[key] = key;
    }
}

PruneStale(zh, allKeys);

if (options.DryRun)
{
    Console.WriteLine("[dry-run] 不写出文件。");
}
else
{
    ResourceMerger.WriteFlat(zhPath, zh);
    ResourceMerger.WriteFlat(enPath, en);
    Console.WriteLine($"已写出 {zhPath}（{zh.Count} 条）、{enPath}（{en.Count} 条）。");
}

Console.WriteLine($"en-US 合并：新增 {enResult.Added.Count}，剪除 {enResult.Removed.Count}，保留翻译 {enResult.Kept}。");
ReportInvalid(zhPath, zhInvalid);
ReportInvalid(enPath, enInvalid);

if (options.ReportPath != null)
{
    WriteReport(options, files.Count, literalEntries, interpolatedEntries, unresolvedEntries, enResult);
    Console.WriteLine($"报告已写出 {options.ReportPath}");
}

PrintUnresolved(unresolvedEntries);
PrintInterpolatedHint(interpolatedEntries);
return 0;

static IEnumerable<string> EnumerateSourceFiles(string root)
{
    return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
        .Where(path =>
        {
            var normalized = path.Replace('\\', '/');
            return !normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
                && !normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase);
        });
}

static void PruneStale(SortedDictionary<string, string> resources, IReadOnlyList<string> keys)
{
    var live = keys.ToHashSet(StringComparer.Ordinal);
    var stale = resources.Keys.Where(key => !live.Contains(key)).ToList();
    foreach (var key in stale)
    {
        resources.Remove(key);
    }
}

static void ReportInvalid(string path, Collection<string> invalidEntries)
{
    if (invalidEntries.Count > 0)
    {
        Console.WriteLine($"[warn] {path} 中 {invalidEntries.Count} 个条目的值不是字符串，已被丢弃：{string.Join("、", invalidEntries.Take(5))}...");
    }
}

static void PrintUnresolved(IReadOnlyList<BusinessMessageEntry> unresolved)
{
    if (unresolved.Count == 0)
    {
        return;
    }

    Console.WriteLine();
    Console.WriteLine($"未解析的消息表达式（{unresolved.Count} 处，需人工确认）：");
    foreach (var entry in unresolved)
    {
        Console.WriteLine($"  {entry.File}:{entry.Line}  {entry.Expression}");
    }
}

static void PrintInterpolatedHint(IReadOnlyList<BusinessMessageEntry> interpolated)
{
    if (interpolated.Count == 0)
    {
        return;
    }

    Console.WriteLine();
    Console.WriteLine($"插值字符串消息（{interpolated.Count} 处）：运行时 Message 已填充参数，按 key 翻译不会命中；如需翻译，请把抛出点改写为格式串 + Argments，例如 new BusinessException(\"最多 {{0}} 个.\", count)。");
    foreach (var entry in interpolated)
    {
        Console.WriteLine($"  {entry.File}:{entry.Line}  {entry.Template}");
    }
}

static void WriteReport(
    ScanOptions options,
    int fileCount,
    IReadOnlyList<BusinessMessageEntry> literals,
    IReadOnlyList<BusinessMessageEntry> interpolated,
    IReadOnlyList<BusinessMessageEntry> unresolved,
    ResourceMergeResult enResult)
{
    var builder = new StringBuilder();
    builder.AppendLine("# BusinessException i18n 扫描报告");
    builder.AppendLine();
    builder.AppendLine($"- 时间：{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
    builder.AppendLine($"- 源码目录：{options.SourceDirectory}");
    builder.AppendLine($"- 输出目录：{options.OutputDirectory}");
    builder.AppendLine($"- 源文件数：{fileCount}，构造点：{literals.Count + interpolated.Count + unresolved.Count}");
    builder.AppendLine($"- 字面量 key：{literals.Select(e => e.Template).Distinct().Count()}，插值模板：{interpolated.Select(e => e.Template).Distinct().Count()}，未解析：{unresolved.Count}");
    builder.AppendLine($"- en-US 合并：新增 {enResult.Added.Count}，剪除 {enResult.Removed.Count}，保留翻译 {enResult.Kept}");
    builder.AppendLine();

    if (interpolated.Count > 0)
    {
        builder.AppendLine($"## 插值字符串消息（{interpolated.Count} 处，运行时按 key 匹配不会命中，需改写抛出点为格式串 + Argments）");
        builder.AppendLine();
        foreach (var entry in interpolated)
        {
            builder.AppendLine($"- {entry.File}:{entry.Line} `{entry.Template}`");
        }

        builder.AppendLine();
    }

    if (unresolved.Count > 0)
    {
        builder.AppendLine($"## 未解析的消息表达式（{unresolved.Count} 处）");
        builder.AppendLine();
        foreach (var entry in unresolved)
        {
            builder.AppendLine($"- {entry.File}:{entry.Line} `{entry.Expression}`");
        }

        builder.AppendLine();
    }

    if (enResult.Removed.Count > 0)
    {
        builder.AppendLine($"## 本次剪除的 key（{enResult.Removed.Count} 个）");
        builder.AppendLine();
        foreach (var key in enResult.Removed)
        {
            builder.AppendLine($"- `{key}`");
        }

        builder.AppendLine();
    }

    File.WriteAllText(options.ReportPath!, builder.ToString(), new UTF8Encoding(false));
}
