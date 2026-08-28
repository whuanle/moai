using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql.EntityFrameworkCore.PostgreSQL.Design.Internal;
using Npgsql.EntityFrameworkCore.PostgreSQL.Scaffolding.Internal;
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

        try
        {
            var projectDir = ResolveProjectDirectory();
            Directory.SetCurrentDirectory(projectDir);
            PrintInfo($"当前工作目录: {projectDir}");

            var db = BuildDatabaseRoute(projectDir);

            var connectionString = LoadConnectionString(projectDir);
            ScaffoldDatabaseModel(connectionString, db, projectDir);

            DistributeToSolution(db, projectDir);

            PrintSuccess(db.DatabaseDir);
        }
        catch (Exception ex)
        {
            PrintError(ex.Message);
            PrintError(ex.StackTrace!);
        }

        await Task.CompletedTask;
    }

    /// <summary>把生成的实体、配置文件和 DbContext 分发到目标项目。</summary>
    private static void DistributeToSolution(DatabaseRoute db, string projectDir)
    {
        var slnDir = Directory.GetParent(projectDir)!.Parent!.FullName;
        var sharedDir = Path.Combine(slnDir, "src", "database", "MoAI.Database.Shared");
        var postgresDir = Path.Combine(slnDir, "src", "database", "MoAI.Database.Postgres");

        var sharedEntitiesDir = Path.Combine(sharedDir, "Entities");
        var postgresDataDir = Path.Combine(postgresDir, "Data");

        Console.WriteLine();
        Console.WriteLine("正在分发到解决方案...");

        // 1. 先清空目标目录，再复制各自的 .cs 文件（实体文件需补 Entity 后缀）
        CopyDirectory(db.EntitiesDir, sharedEntitiesDir, "实体文件", addEntitySuffix: true);
        CopyDirectory(db.DataDir, postgresDataDir, "配置文件", addEntitySuffix: false);

        // 2. 复制 DatabaseContext 到 MoAI.Database.Shared
        var contextFile = Path.Combine(db.DatabaseDir, "DatabaseContext.cs");
        if (File.Exists(contextFile))
        {
            File.Copy(contextFile, Path.Combine(sharedDir, Path.GetFileName(contextFile)), true);
            PrintInfo($"  DatabaseContext: {Path.GetFileName(contextFile)}");
        }
    }

    /// <summary>清空目标目录并将源目录下的 .cs 文件复制到目标目录。</summary>
    private static void CopyDirectory(string sourceDir, string targetDir, string label, bool addEntitySuffix)
    {
        Console.WriteLine($"正在复制{label}...");

        if (Directory.Exists(targetDir))
        {
            Directory.Delete(targetDir, true);
        }

        Directory.CreateDirectory(targetDir);

        foreach (var file in Directory.GetFiles(sourceDir, "*.cs"))
        {
            // 需要 Entity 后缀时，先把源目录里的文件本身重命名，再复制
            var sourcePath = file;
            if (addEntitySuffix)
            {
                var renamed = EnsureEntitySuffix(Path.GetFileName(file));
                if (renamed != Path.GetFileName(file))
                {
                    sourcePath = Path.Combine(sourceDir, renamed);
                    File.Move(file, sourcePath);
                }
            }

            File.Copy(sourcePath, Path.Combine(targetDir, Path.GetFileName(sourcePath)), true);
            PrintInfo($"  {Path.GetFileName(sourcePath)}");
        }
    }

    /// <summary>若文件名不含 Entity 后缀，则在 .cs 前补上。</summary>
    private static string EnsureEntitySuffix(string fileName)
    {
        return fileName.EndsWith("Entity.cs", StringComparison.CurrentCultureIgnoreCase)
            ? fileName
            : fileName[..^3] + "Entity.cs";
    }

    /// <summary>通过 EF Core Design 服务连接数据库并生成模型。</summary>
    private static void ScaffoldDatabaseModel(
        string connectionString,
        DatabaseRoute db,
        string projectDir)
    {
        Console.WriteLine("开始从数据库生成实体代码...");
        Console.WriteLine($"连接字符串: {connectionString}");

        var services = new ServiceCollection();

        // 添加 EF Core Design 服务
        services.AddEntityFrameworkDesignTimeServices();

        // 添加 PostgreSQL Provider 的 Design 服务
#pragma warning disable EF1001 // Internal EF Core API usage.
        new NpgsqlDesignTimeServices().ConfigureDesignTimeServices(services);

        // 添加操作报告器
        services.AddSingleton<IOperationReporter, ConsoleOperationReporter>();

        var scaffolder = services.BuildServiceProvider()
            .GetRequiredService<IReverseEngineerScaffolder>();

        var codeOptions = new ModelCodeGenerationOptions
        {
            ContextName = "DatabaseContext",
            ContextDir = db.DataDir,
            ContextNamespace = "MoAI.Database",
            ModelNamespace = "MoAI.Database.Entities",
            RootNamespace = "MoAI.Database",
            SuppressConnectionStringWarning = true,
            SuppressOnConfiguring = true,
            UseDataAnnotations = false,
            UseNullableReferenceTypes = true,
            ProjectDir = projectDir,  // T4 模板会从 ProjectDir/CodeTemplates/EFCore 目录加载
        };

        Console.WriteLine($"T4 模板目录: {Path.Combine(projectDir, "CodeTemplates", "EFCore")}");
        Console.WriteLine("正在连接数据库并生成模型...");

        var scaffoldedModel = scaffolder.ScaffoldModel(
            connectionString,
            new DatabaseModelFactoryOptions(tables: null, schemas: null),
            new ModelReverseEngineerOptions(),
            codeOptions);

        // DatabaseContext 要往上提一级
        scaffoldedModel.ContextFile.Path = Path.Combine(
            Directory.GetParent(scaffoldedModel.ContextFile.Path)!.Parent!.FullName,
            Path.GetFileName(scaffoldedModel.ContextFile.Path));

        Console.WriteLine("正在保存生成的代码...");
        var savedFiles = scaffolder.Save(
            scaffoldedModel,
            db.EntitiesDir,
            overwriteFiles: true);

        PrintInfo($"DbContext 文件: {savedFiles.ContextFile}");
        PrintInfo($"生成的实体文件数: {savedFiles.AdditionalFiles.Count}");
    }

    /// <summary>删除 Shared/Entities 目录中已有的旧实体文件。</summary>
    /// <summary>载入数据库连接字符串。</summary>
    private static string LoadConnectionString(string projectDir)
    {
        var configuration = new ConfigurationBuilder()
            .ApplySourceConfiguration(projectDir)
            .Build();

        var connectionString = configuration["MoAI:Database"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("未找到数据库连接字符串配置 (MoAI:Database)");
        }

        return connectionString.EndsWith(';') ? connectionString : connectionString + ";";
    }

    /// <summary>进入并准备 Database 目录。</summary>
    private static DatabaseRoute BuildDatabaseRoute(string projectDir)
    {
        var db = DatabaseRoute.Create(projectDir);

        if (Directory.Exists(db.DatabaseDir))
        {
            Directory.Delete(db.DatabaseDir, true);
            Console.WriteLine("已删除 Database 目录");
        }

        Directory.CreateDirectory(db.DatabaseDir);
        Directory.CreateDirectory(db.DataDir);
        Directory.CreateDirectory(db.EntitiesDir);

        return db;
    }

    /// <summary>获取项目根目录，避免文件落在 bin 目录下。</summary>
    private static string ResolveProjectDirectory()
    {
        var dir = Directory.GetParent(typeof(Program).Assembly.Location);
        if (dir!.FullName.Contains("bin", StringComparison.CurrentCultureIgnoreCase))
        {
            dir = dir.Parent!.Parent!.Parent;
        }

        return dir!.FullName;
    }

    private static void PrintSuccess(string databaseDir)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine();
        Console.WriteLine("✓ 代码生成完成!");
        Console.WriteLine($"  - 生成目录: {databaseDir}");
        Console.ResetColor();
    }

    private static void PrintInfo(string message) => Console.WriteLine(message);

    private static void PrintError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"错误: {message}");
        Console.ResetColor();
    }

    /// <summary>工作目录相关的路径集合。</summary>
    private sealed record DatabaseRoute(string DatabaseDir, string DataDir, string EntitiesDir)
    {
        public static DatabaseRoute Create(string projectDir) => new(
            DatabaseDir: Path.Combine(projectDir, "Database"),
            DataDir: Path.Combine(projectDir, "Database", "Data"),
            EntitiesDir: Path.Combine(projectDir, "Database", "Entities"));
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

/// <summary>
/// 根据配置文件类型载入系统配置.
/// </summary>
internal static class SourceConfigurationExtensions
{
    /// <summary>导入系统配置.</summary>
    public static IConfigurationBuilder ApplySourceConfiguration(
        this IConfigurationBuilder configurationBuilder,
        string projectDirectory)
    {
        var configurationFilePath = Path.Combine(
            Directory.GetParent(projectDirectory)!.Parent!.FullName,
            "src", "MoAI", "appsettings.Development.json");

        var fileType = Path.GetExtension(configurationFilePath);
        if (".json".Equals(fileType, StringComparison.OrdinalIgnoreCase))
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

        return configurationBuilder;
    }
}
