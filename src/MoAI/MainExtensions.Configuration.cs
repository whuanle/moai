using MoAI.Infra;

namespace MoAI;

/// <summary>
/// MainExtensions.
/// </summary>
public static partial class MainExtensions
{
    // 初始化目录
    private static void InitConfigurationDirectory()
    {
        //if (!Directory.Exists(AppConst.ConfigsTemplate))
        //{
        //    throw new DirectoryNotFoundException("Configuration template directory not found: " + AppConst.ConfigsTemplate);
        //}

        //Directory.CreateDirectory(AppConst.ConfigsPath);

        //var configTemplateSystemPath = Path.Combine(AppConst.ConfigsTemplate, "system.json");

        //var systemConfigPath = Path.Combine(AppConst.ConfigsPath, "system.json");

        //if (!File.Exists(configTemplateSystemPath))
        //{
        //    throw new FileNotFoundException("Default system configuration file not found.", configTemplateSystemPath);
        //}

        //File.Copy(configTemplateSystemPath, systemConfigPath, overwrite: true);
    }

    // 导入系统配置.
    private static void ImportSystemConfiguration(this IHostApplicationBuilder builder)
    {
        // 指定环境变量从文件导入配置
        var configurationFilePath = Environment.GetEnvironmentVariable("MAI_FILE");
        if (string.IsNullOrWhiteSpace(configurationFilePath) || !File.Exists(configurationFilePath))
        {
            configurationFilePath = Path.Combine(AppConst.ConfigsPath, "system.json");
        }

        string? fileType = Path.GetExtension(configurationFilePath);

        if (".json".Equals(fileType, comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            builder.Configuration.AddJsonFile(configurationFilePath, optional: true);
        }
        else if (".yaml".Equals(fileType, StringComparison.OrdinalIgnoreCase))
        {
            builder.Configuration.AddYamlFile(configurationFilePath, optional: true);
        }
        else if (".conf".Equals(fileType, StringComparison.OrdinalIgnoreCase))
        {
            builder.Configuration.AddIniFile(configurationFilePath, optional: true);
        }
#if !DEBUG
        else
        {
            throw new ArgumentException($"The current file type cannot be imported,`MAI_FILE={configurationFilePath}`.");
        }
#endif
    }
}
