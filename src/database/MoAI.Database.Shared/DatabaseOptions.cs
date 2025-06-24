using System.Reflection;

namespace MoAI.Database;

public class DatabaseOptions
{
    /// <summary>
    /// 配置类程序集位置.
    /// </summary>
    public Assembly ConfigurationAssembly { get; init; }

    /// <summary>
    /// 实体类程序集位置.
    /// </summary>
    public Assembly EntityAssembly { get; init; }
}