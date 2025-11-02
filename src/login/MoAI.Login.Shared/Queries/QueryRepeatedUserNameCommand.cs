using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Login.Queries;

/// <summary>
/// 检查用户名是否重复.
/// </summary>
public class QueryRepeatedUserNameCommand : IRequest<SimpleBool>
{
    /// <summary>
    /// 用户名.
    /// </summary>
    public string UserName { get; set; } = string.Empty;
}