using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MoAI.Database;
using MoAI.Database.Schema;
using Npgsql;

namespace MoAI.SchemaSync;

/// <summary>
/// 临时探针：在真实 PostgreSQL 上验证 SchemaReconciler 能从空库建全表，并把被破坏的表/列/索引补回来.
/// </summary>
internal static class Probe
{
    private const string ProbeDatabase = "moai_schema_probe";

    public static async Task<int> RunAsync(string adminConnectionString)
    {
        var adminBuilder = new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = "postgres" };

        await DropDatabaseAsync(adminBuilder);
        await CreateDatabaseAsync(adminBuilder);

        var probeBuilder = new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = ProbeDatabase };
        var probeConnectionString = probeBuilder.ConnectionString;

        try
        {
            using (var dbContext = CreateContext(probeConnectionString))
            {
                var empty = SchemaReconciler.Apply(dbContext, NullLogger.Instance);
                Console.WriteLine($"[empty] {empty.CreatedTables.Count} tables, {empty.AddedColumns.Count} columns, {empty.CreatedIndexes.Count} indexes");
            }

            var afterEmpty = await CountAsync(probeConnectionString);
            Console.WriteLine($"[empty] catalog: {afterEmpty.Tables} tables / {afterEmpty.Columns} columns / {afterEmpty.Indexes} indexes");
            Assert(afterEmpty.Tables >= 70, "empty db should get all tables");
            Assert(afterEmpty.Columns > 900, "empty db should get all columns");

            // 模拟线上旧库：整表缺失 + 缺列 + 缺索引
            await ExecuteAsync(probeConnectionString, """
                DROP TABLE dingtalk_app CASCADE;
                DROP TABLE wiki_source_document CASCADE;
                ALTER TABLE access_app DROP COLUMN level;
                ALTER TABLE access_app DROP COLUMN app_type;
                ALTER TABLE publication_review DROP COLUMN publish_version;
                ALTER TABLE notification_log DROP COLUMN recipients;
                DROP INDEX idx_teamres_key_team_id;
                ALTER TABLE access_app ALTER COLUMN name TYPE character varying(20);
                """);

            using (var dbContext = CreateContext(probeConnectionString))
            {
                var repaired = SchemaReconciler.Apply(dbContext, NullLogger.Instance);
                Console.WriteLine($"[repair] {repaired.CreatedTables.Count} tables ({string.Join(",", repaired.CreatedTables)}), {repaired.AddedColumns.Count} columns ({string.Join(",", repaired.AddedColumns)}), {repaired.WidenedColumns.Count} widened ({string.Join(",", repaired.WidenedColumns)}), {repaired.CreatedIndexes.Count} indexes");
                Assert(repaired.CreatedTables.Contains("dingtalk_app"), "dingtalk_app should be recreated");
                Assert(repaired.AddedColumns.Contains("publication_review.publish_version"), "publish_version should be added");
                Assert(repaired.AddedColumns.Contains("access_app.level"), "access_app.level should be added");
                Assert(repaired.CreatedIndexes.Contains("idx_teamres_key_team_id"), "index should be recreated");
            }

            var afterRepair = await CountAsync(probeConnectionString);
            Console.WriteLine($"[repair] catalog: {afterRepair.Tables} tables / {afterRepair.Columns} columns / {afterRepair.Indexes} indexes");
            Assert(afterRepair.Tables == afterEmpty.Tables, "table count restored");
            Assert(afterRepair.Columns == afterEmpty.Columns, $"column count restored ({afterRepair.Columns} vs {afterEmpty.Columns})");
            Assert(afterRepair.Indexes == afterEmpty.Indexes, "index count restored");

            // 再跑一次应零变更（幂等）
            using (var dbContext = CreateContext(probeConnectionString))
            {
                var second = SchemaReconciler.Apply(dbContext, NullLogger.Instance);
                Console.WriteLine($"[idempotent] {second.CreatedTables.Count} tables, {second.AddedColumns.Count} columns, {second.CreatedIndexes.Count} indexes");
                Assert(!second.HasChanges, "second run should be a no-op");
            }

            // 生成的脚本也应在「当前库」上整体可执行（全量手工执行路径）
            using (var dbContext = CreateContext(probeConnectionString))
            {
                var createScript = dbContext.Database.GenerateCreateScript();
                var tables = SchemaScriptParser.Parse(createScript);
                var patch = SchemaDdl.BuildIdempotentScript(createScript, tables);
                await ExecuteScriptAsync(probeConnectionString, patch);
                Console.WriteLine("[script] full idempotent patch executed without error");
            }

            Console.WriteLine("PROBE PASS");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("PROBE FAIL: " + ex);
            return 1;
        }
        finally
        {
            await DropDatabaseAsync(adminBuilder);
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static PostgresDatabaseContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<PostgresDatabaseContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new PostgresDatabaseContext(options, new ServiceCollection().BuildServiceProvider());
    }

    private static async Task<(int Tables, int Columns, int Indexes)> CountAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var tables = await ScalarAsync(connection, "SELECT count(*)::int FROM information_schema.tables WHERE table_schema = current_schema() AND table_type = 'BASE TABLE' AND table_name NOT LIKE 'pg_%'");
        var columns = await ScalarAsync(connection, "SELECT count(*)::int FROM information_schema.columns WHERE table_schema = current_schema() AND table_name NOT LIKE 'pg_%'");
        var indexes = await ScalarAsync(connection, "SELECT count(*)::int FROM pg_indexes WHERE schemaname = current_schema() AND indexname NOT LIKE 'pg_%'");
        return (tables, columns, indexes);
    }

    private static async Task<int> ScalarAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteScriptAsync(string connectionString, string script)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var index = 0;
        foreach (var statement in SchemaScriptParser.SplitStatements(script))
        {
            index++;
            try
            {
                await using var command = new NpgsqlCommand(statement, connection);
                command.CommandTimeout = 120;
                await command.ExecuteNonQueryAsync();
            }
            catch (PostgresException ex)
            {
                throw new InvalidOperationException($"statement #{index} failed ({ex.SqlState}): {statement[..Math.Min(statement.Length, 200)]}", ex);
            }
        }
    }

    private static async Task DropDatabaseAsync(NpgsqlConnectionStringBuilder admin)
    {
        await using var connection = new NpgsqlConnection(admin.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{ProbeDatabase}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task CreateDatabaseAsync(NpgsqlConnectionStringBuilder admin)
    {
        await using var connection = new NpgsqlConnection(admin.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{ProbeDatabase}\"", connection);
        await command.ExecuteNonQueryAsync();
    }
}
