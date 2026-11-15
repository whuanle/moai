using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MoAI.Database;
using MoAI.Database.Schema;
using MoAI.SchemaSync;

// 用法：
//   dotnet run --project tool/SchemaSync                 # 由模型生成 asserts/schema-sync.sql（存量库幂等补齐脚本）
//   dotnet run --project tool/SchemaSync -- script <路径>  # 同上，指定输出文件
//   dotnet run --project tool/SchemaSync -- apply <连接串> # 直接对目标库执行一次 schema 补全（等价于应用启动时的自动补齐）
var command = args.Length > 0 ? args[0] : "script";

switch (command)
{
    case "script":
        {
            using var dbContext = CreateContext(DatabaseUrl);
            var createScript = dbContext.Database.GenerateCreateScript();
            var tables = SchemaScriptParser.Parse(createScript);
            var patch = SchemaDdl.BuildIdempotentScript(createScript, tables);

            var target = args.Length > 1 ? args[1] : Path.Combine(ResolveRepositoryRoot(), "asserts", "schema-sync.sql");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(target))!);
            File.WriteAllText(target, patch);

            Console.WriteLine($"tables: {tables.Count}");
            Console.WriteLine($"columns: {tables.Sum(x => x.Columns.Count)}");
            Console.WriteLine($"indexes: {tables.Sum(x => x.IndexStatements.Count)}");
            Console.WriteLine($"written: {Path.GetFullPath(target)} ({patch.Length} chars)");
            break;
        }

    case "apply":
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("usage: SchemaSync apply <connectionString>");
                return 1;
            }

            using var dbContext = CreateContext(args[1]);
            var result = SchemaReconciler.Apply(dbContext, NullLogger.Instance);
            PrintResult(result);
            break;
        }

    case "diff":
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("usage: SchemaSync diff <connectionString>");
                return 1;
            }

            using var dbContext = CreateContext(args[1]);
            var result = SchemaReconciler.Apply(dbContext, NullLogger.Instance, dryRun: true);
            PrintResult(result);
            Console.WriteLine(result.HasChanges ? "schema drift detected" : "schema up to date");
            break;
        }

    case "probe":
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("usage: SchemaSync probe <adminConnectionString>");
                return 1;
            }

            return await Probe.RunAsync(args[1]);
        }

    default:
        Console.Error.WriteLine("usage: SchemaSync [script [path] | apply <connectionString> | probe <adminConnectionString>]");
        return 1;
}

return 0;

static PostgresDatabaseContext CreateContext(string connectionString)
{
    var options = new DbContextOptionsBuilder<PostgresDatabaseContext>()
        .UseNpgsql(connectionString)
        .Options;

    return new PostgresDatabaseContext(options, new ServiceCollection().BuildServiceProvider());
}

static void PrintResult(SchemaSyncResult result)
{
    Console.WriteLine($"created tables: {result.CreatedTables.Count} [{string.Join(", ", result.CreatedTables)}]");
    Console.WriteLine($"added columns: {result.AddedColumns.Count} [{string.Join(", ", result.AddedColumns)}]");
    Console.WriteLine($"widened columns: {result.WidenedColumns.Count} [{string.Join(", ", result.WidenedColumns)}]");
    Console.WriteLine($"created indexes: {result.CreatedIndexes.Count}");
    foreach (var warning in result.Warnings)
    {
        Console.WriteLine($"warning: {warning}");
    }
}

static string ResolveRepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "MoAI.sln")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    return Directory.GetCurrentDirectory();
}

internal static partial class Program
{
    // 仅用于生成脚本/离线补全的连接串，不对应任何真实库（GenerateCreateScript 不需要连库）
    internal const string DatabaseUrl = "Host=127.0.0.1;Database=moai_schema_sync;Username=postgres;Password=postgres";
}
