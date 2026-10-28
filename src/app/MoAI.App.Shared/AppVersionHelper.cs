using System;
using System.Linq;

namespace MoAI.App;

/// <summary>
/// 应用版本号 <c>aaa.bb.cc</c> 文本与 int 编码（aaa*10000 + bb*100 + cc）互转；int 编码可直接比较大小.
/// </summary>
public static class AppVersionHelper
{
    /// <summary>
    /// 版本号文本格式：首位 1~3 位数字，后两段各 1~2 位数字.
    /// </summary>
    public const string Pattern = @"^\d{1,3}\.\d{1,2}\.\d{1,2}$";

    /// <summary>
    /// 解析 <c>aaa.bb.cc</c> 文本为 int 编码；格式非法返回 false.
    /// </summary>
    /// <param name="text">版本号文本.</param>
    /// <param name="version">解析结果编码.</param>
    /// <returns>是否解析成功.</returns>
    public static bool TryParse(string? text, out int version)
    {
        version = 0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var parts = text.Split('.');
        if (parts.Length != 3 || !TryParsePart(parts[0], 3, 999, out var aaa) || !TryParsePart(parts[1], 2, 99, out var bb) || !TryParsePart(parts[2], 2, 99, out var cc))
        {
            return false;
        }

        version = (aaa * 10000) + (bb * 100) + cc;
        return true;
    }

    /// <summary>
    /// 把 int 编码格式化为 <c>aaa.bb.cc</c> 文本.
    /// </summary>
    /// <param name="version">版本号编码.</param>
    /// <returns>版本号文本.</returns>
    public static string Format(int version)
    {
        var aaa = version / 10000;
        var bb = version / 100 % 100;
        var cc = version % 100;
        return $"{aaa}.{bb}.{cc}";
    }

    private static bool TryParsePart(string text, int maxDigits, int maxValue, out int value)
    {
        value = 0;
        if (text.Length == 0 || text.Length > maxDigits || !text.All(char.IsAsciiDigit) || !int.TryParse(text, out value))
        {
            return false;
        }

        return value <= maxValue;
    }
}
