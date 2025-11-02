
#pragma warning disable CA1052 // 静态容器类型应为 Static 或 NotInheritable
namespace MoAI.Infra.Models;

/// <summary>
/// 空数据.
/// </summary>
public class EmptyCommandResponse
{
    /// <summary>
    /// 默认实例.
    /// </summary>
    public static readonly EmptyCommandResponse Default = new EmptyCommandResponse();

    private EmptyCommandResponse()
    {
    }
}
