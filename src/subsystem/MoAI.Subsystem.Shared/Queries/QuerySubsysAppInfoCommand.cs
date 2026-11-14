using FluentValidation;
using MediatR;
using MoAI.Subsystem.Queries.Responses;

namespace MoAI.Subsystem.Queries;

/// <summary>
/// 系统级接入自省查询（机器面 GET /api/subsys/info，服务端按调用上下文回填 id）.
/// </summary>
public class QuerySubsysAppInfoCommand : IRequest<QuerySubsysAppInfoCommandResponse>, IModelValidator<QuerySubsysAppInfoCommand>
{
    /// <summary>
    /// 接入 id（由服务端回填）.
    /// </summary>
    public Guid SubsysAppId { get; set; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QuerySubsysAppInfoCommand> validate)
    {
    }
}
