using System.Text.Json.Serialization;
using FluentValidation;
using Mediator;

namespace MoAI.Subsystem.Queries;

/// <summary>
/// 查询事件目录（机器可读的事件名/受众/载荷字段），供接入方生成订阅与校验规则.
/// </summary>
public class QuerySubsysEventCatalogCommand : IRequest<Responses.QuerySubsysEventCatalogCommandResponse>, IModelValidator<QuerySubsysEventCatalogCommand>
{
    /// <summary>接入 id，由 Controller 从调用方上下文回填（仅做身份校验）.</summary>
    [JsonIgnore]
    public Guid SubsystemId { get; set; }

    /// <inheritdoc/>
    public static void Validate(AbstractValidator<QuerySubsysEventCatalogCommand> validate)
    {
        validate.RuleFor(x => x.SubsystemId).NotEmpty().WithMessage("接入 id 不正确.");
    }
}
