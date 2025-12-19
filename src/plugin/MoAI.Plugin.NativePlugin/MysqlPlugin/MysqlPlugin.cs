#pragma warning disable CA1822 // 将成员标记为 static
#pragma warning disable CA1031 // 不捕获常规异常类型

using Maomi;
using Microsoft.SemanticKernel;
using MoAI.Infra.Exceptions;
using MoAI.Infra.System.Text.Json;
using MoAI.Plugin.Models;
using MySqlConnector;
using System.ComponentModel;
using System.Data;
using System.Text.Json;

namespace MoAI.Plugin.Plugins.MysqlPlugin;

/// <summary>
/// MySQL 插件，用于执行 SQL 查询并返回 JSON 格式的结果。
/// </summary>
[Attributes.NativePluginConfig(
    "mysql_plugin",
    Name = "MySQL 数据库插件",
    Description = "执行 SQL 查询并返回 JSON 格式的结果",
    Classify = NativePluginClassify.Tool,
    ConfigType = typeof(MysqlPluginConfig))]
[InjectOnTransient]
public class MysqlPlugin : INativePluginRuntime
{
    private MysqlPluginConfig _config = default!;

    /// <inheritdoc/>
    public static string GetParamsExampleValue()
    {
        return "SELECT * FROM your_table_name LIMIT 10;";
    }

    /// <inheritdoc/>
    public async Task<string?> CheckConfigAsync(string config)
    {
        await Task.CompletedTask;
        try
        {
            var objectParams = JsonSerializer.Deserialize<MysqlPluginConfig>(config, JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping);
            if (string.IsNullOrWhiteSpace(objectParams?.ConnectionString))
            {
                return "数据库连接字符串不能为空";
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            return $"配置解析失败: {ex.Message}";
        }
    }

    /// <inheritdoc/>
    public async Task ImportConfigAsync(string config)
    {
        await Task.CompletedTask;
        _config = JsonSerializer.Deserialize<MysqlPluginConfig>(config)!;
    }

    /// <summary>
    /// 执行 SQL 查询并返回 JSON 格式的结果。
    /// </summary>
    /// <param name="sql">SQL 查询字符串</param>
    /// <returns>JSON 格式的查询结果</returns>
    [KernelFunction("invoke")]
    [Description("执行 SQL 查询并返回 JSON 格式的结果")]
    public async Task<string> InvokeAsync([Description("SQL 查询字符串")] string sql)
    {
        try
        {
            using var connection = new MySqlConnection(_config.ConnectionString);
            await connection.OpenAsync();

#pragma warning disable CA2100 // 检查 SQL 查询是否存在安全漏洞
            using var command = new MySqlCommand(sql, connection);
#pragma warning restore CA2100 // 检查 SQL 查询是否存在安全漏洞
            using var reader = await command.ExecuteReaderAsync();

            var results = new List<Dictionary<string, object>>();
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.GetValue(i);
                }

                results.Add(row);
            }

            return JsonSerializer.Serialize(results, JsonSerializerOptionValues.UnsafeRelaxedJsonEscaping);
        }
        catch (Exception ex)
        {
            throw new BusinessException($"SQL 执行失败: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<string> TestAsync(string @params)
    {
        return await InvokeAsync(@params);
    }
}
