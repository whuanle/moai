using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MoAI.Database.Seed;
using MoAI.Infra.Exceptions;

namespace MoAI.Settings.Services;

/// <summary>
/// 知识库允许上传文件类型（逗号分隔扩展名白名单）的解析与归一，供保存校验与读取服务共用.
/// </summary>
public static partial class WikiFileTypes
{
    /// <summary>
    /// 允许配置的最大扩展名条数.
    /// </summary>
    public const int MaxCount = 100;

    /// <summary>
    /// 单个扩展名的合法形态：以点号开头，1~16 位字母或数字（归一为小写后校验）.
    /// </summary>
    [GeneratedRegex(@"^\.[a-z0-9]{1,16}$")]
    private static partial Regex ExtensionRegex();

    /// <summary>
    /// 内置默认允许类型（取 <see cref="SettingDefinitions"/> 中 <see cref="SettingDefinitions.WikiAllowedFileTypesKey"/> 的默认值）.
    /// </summary>
    public static IReadOnlyList<string> Default { get; } = ParseOrDefault(null);

    /// <summary>
    /// 严格解析并归一：小写、去重、剔除空白项；为空或存在任一非法项时返回 false.
    /// </summary>
    /// <param name="value">逗号分隔的扩展名配置.</param>
    /// <param name="fileTypes">归一后的扩展名列表.</param>
    /// <returns>是否全部合法且非空.</returns>
    public static bool TryNormalize(string? value, out IReadOnlyList<string> fileTypes)
    {
        fileTypes = Array.Empty<string>();
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var types = new List<string>();
        foreach (var token in value.Split(','))
        {
            var extension = token.Trim().ToLowerInvariant();
            if (extension.Length == 0)
            {
                continue;
            }

            if (!ExtensionRegex().IsMatch(extension) || types.Contains(extension))
            {
                return false;
            }

            types.Add(extension);
        }

        if (types.Count == 0 || types.Count > MaxCount)
        {
            return false;
        }

        fileTypes = types;
        return true;
    }

    /// <summary>
    /// 保存校验并归一：非法直接抛业务异常（400），合法返回归一后的扩展名列表.
    /// </summary>
    /// <param name="value">逗号分隔的扩展名配置.</param>
    /// <returns>归一后的扩展名列表.</returns>
    public static IReadOnlyList<string> NormalizeOrThrow(string value)
    {
        if (!TryNormalize(value, out var fileTypes))
        {
            throw new BusinessException("知识库允许上传的文件类型格式无效，需为逗号分隔的扩展名（如 .pdf,.docx,.md），每项以点号开头、不超过 16 位字母或数字，最多 100 项且不重复.") { StatusCode = 400 };
        }

        return fileTypes;
    }

    /// <summary>
    /// 读取容错解析：剔除无法识别的项，全部非法或为空时回退内置默认值.
    /// </summary>
    /// <param name="value">逗号分隔的扩展名配置.</param>
    /// <returns>归一后的扩展名列表.</returns>
    public static IReadOnlyList<string> ParseOrDefault(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return FromDefinitionDefault();
        }

        var types = new List<string>();
        foreach (var token in value.Split(','))
        {
            var extension = token.Trim().ToLowerInvariant();
            if (extension.Length > 0 && ExtensionRegex().IsMatch(extension) && !types.Contains(extension))
            {
                types.Add(extension);
            }
        }

        return types.Count > 0 ? types : FromDefinitionDefault();
    }

    private static IReadOnlyList<string> FromDefinitionDefault()
    {
        var defaultValue = SettingDefinitions.Find(SettingDefinitions.WikiAllowedFileTypesKey)?.DefaultValue;
        if (string.IsNullOrWhiteSpace(defaultValue))
        {
            return Array.Empty<string>();
        }

        return defaultValue
            .Split(',')
            .Select(x => x.Trim().ToLowerInvariant())
            .Where(x => x.Length > 0)
            .ToList();
    }
}
