using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Login.Commands;

/// <summary>
/// 触发更新超级管理员列表事件.
/// </summary>
public class RefreshAdminsCommand : IRequest<EmptyCommandResponse>
{
}
