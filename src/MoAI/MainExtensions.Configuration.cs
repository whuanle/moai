// <copyright file="MainExtensions.Configuration.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using MoAI.Infra;

namespace MoAI;

public static partial class MainExtensions
{
    // 初始化目录
    private static void InitConfigurationDirectory()
    {
        if (!Directory.Exists(AppConst.ConfigsTemplate))
        {
            throw new DirectoryNotFoundException("Configuration template directory not found: " + AppConst.ConfigsTemplate);
        }

        Directory.CreateDirectory(AppConst.ConfigsPath);

        var configTemplateSystemPath = Path.Combine(AppConst.ConfigsTemplate, "system.json");

        var systemConfigPath = Path.Combine(AppConst.ConfigsPath, "system.json");

        if (!File.Exists(configTemplateSystemPath))
        {
            throw new FileNotFoundException("Default system configuration file not found.", configTemplateSystemPath);
        }

        File.Copy(configTemplateSystemPath, systemConfigPath, overwrite: true);
    }

    // 导入系统配置.
    private static void ImportSystemConfiguration(this IHostApplicationBuilder builder)
    {
        // 指定环境变量从文件导入配置
        var configurationFilePath = Environment.GetEnvironmentVariable("MAI_FILE");
        if (string.IsNullOrWhiteSpace(configurationFilePath))
        {
            configurationFilePath = Path.Combine(AppConst.ConfigsPath, "system.json");
            return;
        }

        string? fileType = Path.GetExtension(configurationFilePath);
        if (".json".Equals(fileType, comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            builder.Configuration.AddJsonFile(configurationFilePath);
        }
        else if (".yaml".Equals(fileType, StringComparison.OrdinalIgnoreCase))
        {
            builder.Configuration.AddYamlFile(configurationFilePath);
        }
        else if (".conf".Equals(fileType, StringComparison.OrdinalIgnoreCase))
        {
            builder.Configuration.AddIniFile(configurationFilePath);
        }
        else
        {
            throw new ArgumentException($"The current file type cannot be imported,`MAI_FILE={configurationFilePath}`.");
        }
    }
}
