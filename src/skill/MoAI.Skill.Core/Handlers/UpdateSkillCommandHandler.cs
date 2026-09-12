using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Skill.Commands;

namespace MoAI.Skill.Handlers;

/// <summary>
/// <inheritdoc cref="UpdateSkillCommand"/>
/// </summary>
public class UpdateSkillCommandHandler : IRequestHandler<UpdateSkillCommand, EmptyCommandResponse>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateSkillCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public UpdateSkillCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateSkillCommand request, CancellationToken cancellationToken)
    {
        var skill = await _databaseContext.Skills.FirstOrDefaultAsync(x => x.Id == request.SkillId, cancellationToken)
            ?? throw new BusinessException("技能不存在.") { StatusCode = 404 };

        await SkillFilesGuard.EnsureFilesValidAsync(_databaseContext, request.Files, cancellationToken);

        skill.Name = request.Name;
        skill.Description = request.Description ?? string.Empty;
        skill.Instructions = request.Instructions ?? string.Empty;
        skill.Files = JsonSerializer.Serialize(request.Files, JsonOptions);

        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
