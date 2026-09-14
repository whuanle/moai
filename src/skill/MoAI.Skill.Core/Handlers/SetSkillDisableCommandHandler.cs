using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Skill.Commands;

namespace MoAI.Skill.Handlers;

/// <summary>
/// <inheritdoc cref="SetSkillDisableCommand"/>
/// </summary>
public class SetSkillDisableCommandHandler : IRequestHandler<SetSkillDisableCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="SetSkillDisableCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public SetSkillDisableCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(SetSkillDisableCommand request, CancellationToken cancellationToken)
    {
        var skill = await _databaseContext.Skills.FirstOrDefaultAsync(x => x.Id == request.SkillId, cancellationToken)
            ?? throw new BusinessException("技能不存在.") { StatusCode = 404 };

        skill.IsDisable = request.IsDisable;
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
