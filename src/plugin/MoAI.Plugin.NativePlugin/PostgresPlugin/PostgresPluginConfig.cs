using MoAI.Plugin.Attributes;
using System.Text.Json.Serialization;

namespace MoAI.Plugin.Plugins.PostgresPlugin;

/// <summary>
/// PostgreSQL 插件配置类。
/// </summary>
public class PostgresPluginConfig
{
    /// <summary>
    /// 数据库连接字符串。
    /// </summary>
    [JsonPropertyName("ConnectionString")]
    [NativePluginConfigField(
        Key = nameof(ConnectionString),
        Description = "数据库连接字符串",
        FieldType = PluginConfigFieldType.String,
        IsRequired = true,
        ExampleValue = "Host=localhost;Port=5432;Username=myuser;Password=mypassword;Database=mydb;")]
    public string ConnectionString { get; set; } = string.Empty;
}