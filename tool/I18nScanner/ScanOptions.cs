namespace I18nScanner;

/// <summary>
/// 命令行参数.
/// </summary>
public sealed class ScanOptions
{
    /// <summary>
    /// 源码根目录（递归扫描 *.cs，跳过 obj/bin）.
    /// </summary>
    public string SourceDirectory { get; private init; } = string.Empty;

    /// <summary>
    /// i18n JSON 输出目录.
    /// </summary>
    public string OutputDirectory { get; private init; } = string.Empty;

    /// <summary>
    /// 扫描报告输出路径；为 null 则不写报告.
    /// </summary>
    public string? ReportPath { get; private init; }

    /// <summary>
    /// 只打印结果不写出文件.
    /// </summary>
    public bool DryRun { get; private init; }

    /// <summary>
    /// 解析命令行参数.
    /// </summary>
    /// <param name="args">命令行参数.</param>
    /// <returns>解析成功返回选项；失败返回 null.</returns>
    public static ScanOptions? Parse(string[] args)
    {
        string? source = null;
        string? output = null;
        string? report = null;
        var dryRun = false;

        var index = 0;
        while (index < args.Length)
        {
            switch (args[index])
            {
                case "--src":
                    if (!TryNext(args, ref index, out source))
                    {
                        return null;
                    }

                    break;

                case "--out":
                    if (!TryNext(args, ref index, out output))
                    {
                        return null;
                    }

                    break;

                case "--report":
                    if (!TryNext(args, ref index, out report))
                    {
                        return null;
                    }

                    break;

                case "--dry-run":
                    dryRun = true;
                    break;

                default:
                    Console.Error.WriteLine($"未知参数: {args[index]}");
                    return null;
            }

            index++;
        }

        var workingDirectory = Directory.GetCurrentDirectory();
        return new ScanOptions
        {
            SourceDirectory = Path.GetFullPath(source ?? Path.Combine(workingDirectory, "src")),
            OutputDirectory = Path.GetFullPath(output ?? Path.Combine(workingDirectory, "src", "MoAI", "i18n")),
            ReportPath = report != null ? Path.GetFullPath(report) : null,
            DryRun = dryRun
        };
    }

    /// <summary>
    /// 打印用法.
    /// </summary>
    public static void PrintUsage()
    {
        Console.WriteLine("""
            用法: dotnet run --project tool/I18nScanner [--src <源码目录>] [--out <输出目录>] [--report <报告路径>] [--dry-run]

            默认扫描 <当前目录>/src，输出 <当前目录>/src/MoAI/i18n/zh-CN.json 与 en-US.json。
            合并语义：已有翻译保留，新 key 以原文兜底，代码中不再出现的 key 剪除。
            """);
    }

    private static bool TryNext(string[] args, ref int index, out string? value)
    {
        if (index + 1 >= args.Length)
        {
            Console.Error.WriteLine($"参数 {args[index]} 缺少值");
            value = null;
            return false;
        }

        index++;
        value = args[index];
        return true;
    }
}
