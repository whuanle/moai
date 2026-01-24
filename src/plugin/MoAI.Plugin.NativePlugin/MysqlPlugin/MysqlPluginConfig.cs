using MoAI.Plugin.Attributes;
using System.Text.Json.Serialization;

namespace MoAI.Plugin.Plugins.MysqlPlugin;

/// <summary>
/// MySQL 插件配置类。
/// </summary>
public class MysqlPluginConfig
{
    /// <summary>
    /// 数据库连接字符串。
    /// </summary>
    [JsonPropertyName("ConnectionString")]
    [NativePluginField(
        Key = nameof(ConnectionString),
        Description = "数据库连接字符串",
        FieldType = PluginConfigFieldType.String,
        IsRequired = true,
        ExampleValue = "Database=database;Host=127.0.0.1;Password=123456;Port=13306;Username=root")]
    public string ConnectionString { get; set; } = default!;
}