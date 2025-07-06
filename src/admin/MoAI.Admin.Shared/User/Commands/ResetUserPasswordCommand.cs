using MediatR;
using MoAI.Infra.Models;

namespace MoAI.Admin.User.Commands;

public class ResetUserPasswordCommand : IRequest<SimpleString>
{
    public int UserId { get; init; }
}
