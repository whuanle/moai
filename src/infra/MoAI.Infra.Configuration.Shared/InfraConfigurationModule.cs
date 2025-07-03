// <copyright file="InfraConfigurationModule.cs" company="MoAI">
// Copyright (c) MoAI. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// Github link: https://github.com/whuanle/moai
// </copyright>

using Maomi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace MoAI.Infra;

/// <summary>
/// InfraConfigurationModule.
/// </summary>
public class InfraConfigurationModule : IModule
{
    private readonly ILogger _logger;
    private readonly IConfigurationManager _configurationManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="InfraConfigurationModule"/> class.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="configurationManager"></param>
    public InfraConfigurationModule(ILogger<InfraConfigurationModule> logger, IConfigurationManager configurationManager)
    {
        _logger = logger;
        _configurationManager = configurationManager;
    }

    /// <inheritdoc/>
    public void ConfigureServices(ServiceContext context)
    {
        if (!Directory.Exists(AppConst.ConfigsPath))
        {
            InitConfigurationDirectory();
        }

        ImportSystemConfiguration();
        ImportLoggerConfiguration();

        var systemOptions = _configurationManager.Get<SystemOptions>() ?? throw new FormatException("The system configuration cannot be loaded.");
        context.Services.AddSingleton(systemOptions);

        // 注册系统配置
        context.Services.Configure<SystemOptions>(context.Configuration);
        context.Services.AddSingleton(resolver =>
            resolver.GetRequiredService<IOptions<SystemOptions>>().Value);
    }

    private void InitConfigurationDirectory()
    {
        if (!Directory.Exists(AppConst.ConfigsTemplate))
        {
            _logger.LogCritical("Configuration template directory not found: {ConfigsTemplatePath}", AppConst.ConfigsTemplate);
            throw new DirectoryNotFoundException("Configuration template directory not found: " + AppConst.ConfigsTemplate);
        }

        Directory.CreateDirectory(AppConst.ConfigsPath);

        var configTemplateLoggerPath = Path.Combine(AppConst.ConfigsTemplate, "logger.json");
        var configTemplateSystemPath = Path.Combine(AppConst.ConfigsTemplate, "system.json");

        var loggerConfigPath = Path.Combine(AppConst.ConfigsPath, "logger.json");
        var systemConfigPath = Path.Combine(AppConst.ConfigsPath, "system.json");

        if (!File.Exists(configTemplateLoggerPath))
        {
            _logger.LogCritical("Default logger configuration file not found: {DefaultLoggerJsonPath}", configTemplateLoggerPath);
            throw new FileNotFoundException("Default logger configuration file not found.", configTemplateLoggerPath);
        }

        if (!File.Exists(configTemplateSystemPath))
        {
            _logger.LogCritical("Default system configuration file not found: {DefaultSystemJsonPath}", configTemplateSystemPath);
            throw new FileNotFoundException("Default system configuration file not found.", configTemplateSystemPath);
        }

        File.Copy(configTemplateLoggerPath, loggerConfigPath, overwrite: true);
        _logger.LogWarning("Configuration logger file copied to: {LoggerConfigPath}", loggerConfigPath);

        File.Copy(configTemplateSystemPath, systemConfigPath, overwrite: true);
        _logger.LogWarning("Configuration system file copied to: {SystemConfigPath}", systemConfigPath);

        using var rsa = RSA.Create(2048);
        string rsaPrivate = rsa.ExportPkcs8PrivateKeyPem();
        File.WriteAllText(AppConst.PrivateRSA, rsaPrivate);
        _logger.LogWarning("RSA private key file created: {RsaPrivateKeyPath}", AppConst.PrivateRSA);
    }

    // 导入系统配置.
    private void ImportSystemConfiguration()
    {
        // 在调试模式下不导入系统配置文件
#if DEBUG
        return;
#endif

        // 指定环境变量从文件导入配置
        var configurationFilePath = Environment.GetEnvironmentVariable("MAI_CONFIG");
        if (string.IsNullOrWhiteSpace(configurationFilePath))
        {
            configurationFilePath = Path.Combine(AppConst.ConfigsPath, "system.json");
            _logger.LogWarning("Environment variable MAI_CONFIG is not set, using default configuration file: {DefaultConfigPath}", configurationFilePath);
            return;
        }

        string? fileType = Path.GetExtension(configurationFilePath);
        if (".json".Equals(fileType, comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            _configurationManager.AddJsonFile(configurationFilePath);
        }
        else if (".yaml".Equals(fileType, StringComparison.OrdinalIgnoreCase))
        {
            _configurationManager.AddYamlFile(configurationFilePath);
        }
        else if (".conf".Equals(fileType, StringComparison.OrdinalIgnoreCase))
        {
            _configurationManager.AddIniFile(configurationFilePath);
        }
        else
        {
            _logger.LogCritical("The current file type cannot be imported,`MAI_CONFIG={File}`.", configurationFilePath);
            throw new ArgumentException($"The current file type cannot be imported,`MAI_CONFIG={configurationFilePath}`.");
        }
    }

    // 导入日志配置文件.
    private void ImportLoggerConfiguration()
    {
        var loggerTemplateConfigPath = Path.Combine(AppConst.ConfigsTemplate, "logger.json");
        var loggerConfigPath = Path.Combine(AppConst.ConfigsPath, "logger.json");

        if (!File.Exists(loggerConfigPath))
        {
            File.Copy(loggerTemplateConfigPath, loggerConfigPath);
        }

        _logger.LogWarning("Configuration logger file directory: {LoggerConfigPath}", loggerConfigPath);

        _configurationManager.AddJsonFile(loggerConfigPath);
    }
}