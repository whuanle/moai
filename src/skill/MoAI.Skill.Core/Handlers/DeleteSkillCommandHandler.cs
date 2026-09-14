using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Skill.Commands;

namespace MoAI.Skill.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteSkillCommand"/>
/// </summary>
public class DeleteSkillCommandHandler : IRequestHandler<DeleteSkillCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteSkillCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public DeleteSkillCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteSkillCommand request, CancellationToken cancellationToken)
    {
        var skill = await _databaseContext.Skills.FirstOrDefaultAsync(x => x.Id == request.SkillId, cancellationToken)
            ?? throw new BusinessException("技能不存在.") { StatusCode = 404 };

        if (skill.IsSystem)
        {
            throw new BusinessException("系统内置技能不可删除，可使用禁用.") { StatusCode = 400 };
        }

        _databaseContext.Skills.Remove(skill);
        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
