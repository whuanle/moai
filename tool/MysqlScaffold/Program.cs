using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pomelo.EntityFrameworkCore.MySql.Design.Internal;
using System.Text;

namespace MysqlScaffold;

/// <summary>
/// DB first.
/// </summary>
public class Program
{
    private static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        // 获取项目目录，避免文件放在 bin 目录下
        var assemblyDirectory = Directory.GetParent(typeof(Program).Assembly.Location);
        if (assemblyDirectory!.FullName.Contains("bin", StringComparison.CurrentCultureIgnoreCase))
        {
            assemblyDirectory = assemblyDirectory!.Parent!.Parent!.Parent;
        }

        string projectDirectory = assemblyDirectory!.FullName;
        Directory.SetCurrentDirectory(projectDirectory);
        Console.WriteLine("当前工作目录: " + projectDirectory);

        // 清理旧目录，统一放到 Database 目录下
        var databaseDir = Path.Combine(projectDirectory, "Database");
        var dataDir = Path.Combine(databaseDir, "Data");
        var entitiesDir = Path.Combine(databaseDir, "Entities");

        if (Directory.Exists(databaseDir))
        {
            Directory.Delete(databaseDir, true);
            Console.WriteLine("已删除 Database 目录");
        }

        // 创建目录结构
        Directory.CreateDirectory(databaseDir);
        Directory.CreateDirectory(dataDir);
        Directory.CreateDirectory(entitiesDir);

        // 读取配置
        var configurationBuilder = new ConfigurationBuilder();
        ImportSystemConfiguration(projectDirectory, configurationBuilder);
        var configuration = configurationBuilder.Build();

        var connectionString = configuration["MoAI:Database"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("错误: 未找到数据库连接字符串配置 (MoAI:Database)");
            Console.ResetColor();
            return;
        }

        if (!connectionString.EndsWith(';'))
        {
            connectionString += ";";
        }

        connectionString += "GuidFormat=Binary16";

        Console.WriteLine("开始从数据库生成实体代码...");
        Console.WriteLine($"连接字符串: {connectionString}");

        try
        {
            // 配置 EF Core Design 服务
            var services = new ServiceCollection();

            // 添加 EF Core Design 服务
            services.AddEntityFrameworkDesignTimeServices();

            // 添加 MySQL Provider 的 Design 服务
#pragma warning disable EF1001 // Internal EF Core API usage.
            var mySqlDesignTimeServices = new MySqlDesignTimeServices();
            mySqlDesignTimeServices.ConfigureDesignTimeServices(services);

            // 添加操作报告器
            services.AddSingleton<IOperationReporter, ConsoleOperationReporter>();

            var serviceProvider = services.BuildServiceProvider();

            // 获取 scaffolder
            var scaffolder = serviceProvider.GetRequiredService<IReverseEngineerScaffolder>();

            // 配置 scaffold 选项
            var scaffoldOptions = new DatabaseModelFactoryOptions(
                tables: null,
                schemas: null);

            var modelOptions = new ModelReverseEngineerOptions();

            var codeOptions = new ModelCodeGenerationOptions
            {
                ContextName = "DatabaseContext",
                ContextDir = dataDir,
                ContextNamespace = "MoAI.Database",
                ModelNamespace = "MoAI.Database.Entities",
                RootNamespace = "MoAI.Database",
                SuppressConnectionStringWarning = true,
                SuppressOnConfiguring = true,
                UseDataAnnotations = true,
                UseNullableReferenceTypes = true,
                ProjectDir = projectDirectory,  // T4 模板会从 ProjectDir/CodeTemplates/EFCore 目录加载
            };

            Console.WriteLine($"T4 模板目录: {Path.Combine(projectDirectory, "CodeTemplates", "EFCore")}");

            // 执行 scaffold
            Console.WriteLine("正在连接数据库并生成模型...");
            var scaffoldedModel = scaffolder.ScaffoldModel(
                connectionString,
                scaffoldOptions,
                modelOptions,
                codeOptions);

            // 保存生成的代码
            Console.WriteLine("正在保存生成的代码...");

            // DatabaseContext 要往上提一级
            scaffoldedModel.ContextFile.Path = Path.Combine(Directory.GetParent(scaffoldedModel.ContextFile.Path)!.Parent!.FullName, Path.GetFileName(scaffoldedModel.ContextFile.Path));
            var savedFiles = scaffolder.Save(
                scaffoldedModel,
                entitiesDir,
                overwriteFiles: true);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"DbContext 文件: {savedFiles.ContextFile}");
            Console.WriteLine($"生成的实体文件数: {savedFiles.AdditionalFiles.Count}");
            Console.ResetColor();

            // 重命名实体文件，以及重新摆放文件位置
            Console.WriteLine("正在重命名实体文件...");
            foreach (var entityFile in savedFiles.AdditionalFiles)
            {
                var directory = Path.GetDirectoryName(entityFile)!;
                var fileName = Path.GetFileNameWithoutExtension(entityFile);

                var newDir = directory;
                var newFileName = entityFile;

                if (directory.EndsWith("Entities", StringComparison.CurrentCultureIgnoreCase))
                {
                    newDir = directory;
                    newFileName = Path.Combine(directory, fileName + "Entity.cs");
                }

                if (File.Exists(entityFile))
                {
                    File.Move(entityFile, newFileName);
                    Console.WriteLine($"  {Path.GetFileName(entityFile)} -> {Path.GetFileName(newFileName)}");
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n✓ 代码生成完成!");
            Console.WriteLine($"  - DbContext: {savedFiles.ContextFile}");
            Console.WriteLine($"  - 实体目录: {entitiesDir}");
            Console.WriteLine($"  - 请手动复制需要的文件到项目中");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"错误: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Console.ResetColor();
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// 导入系统配置.
    /// </summary>
    private static void ImportSystemConfiguration(string projectDirectory, IConfigurationBuilder configurationBuilder)
    {
        var configurationFilePath = Path.Combine(Directory.GetParent(projectDirectory)!.Parent!.FullName, "src", "MoAI", "appsettings.Development.json");
        if (string.IsNullOrWhiteSpace(configurationFilePath))
        {
            throw new ArgumentException("appsettings.Development.json 未设置或为空");
        }

        string? fileType = Path.GetExtension(configurationFilePath);
        if (".json".Equals(fileType, comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            configurationBuilder.AddJsonFile(configurationFilePath);
        }
        else if (".yaml".Equals(fileType, StringComparison.OrdinalIgnoreCase))
        {
            configurationBuilder.AddYamlFile(configurationFilePath);
        }
        else if (".conf".Equals(fileType, StringComparison.OrdinalIgnoreCase))
        {
            configurationBuilder.AddIniFile(configurationFilePath);
        }
        else
        {
            throw new ArgumentException($"不支持的配置文件类型: `MAI_FILE={configurationFilePath}`");
        }
    }
}

/// <summary>
/// 控制台操作报告器.
/// </summary>
#pragma warning disable EF1001 // Internal EF Core API usage
internal sealed class ConsoleOperationReporter : IOperationReporter
#pragma warning restore EF1001
{
    /// <inheritdoc/>
    public void WriteError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[错误] {message}");
        Console.ResetColor();
    }

    /// <inheritdoc/>
    public void WriteInformation(string message)
    {
        Console.WriteLine($"[信息] {message}");
    }

    /// <inheritdoc/>
    public void WriteVerbose(string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"[详细] {message}");
        Console.ResetColor();
    }

    /// <inheritdoc/>
    public void WriteWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[警告] {message}");
        Console.ResetColor();
    }
}
