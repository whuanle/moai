using MediatR;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Skill.Commands;
using MoAI.Storage.Services;

namespace MoAI.Skill.Handlers;

/// <summary>
/// <inheritdoc cref="CompleteSkillFileCommand"/>
/// </summary>
public class CompleteSkillFileCommandHandler : IRequestHandler<CompleteSkillFileCommand, EmptyCommandResponse>
{
    private readonly IStorageService _storageService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompleteSkillFileCommandHandler"/> class.
    /// </summary>
    /// <param name="storageService">存储领域服务.</param>
    public CompleteSkillFileCommandHandler(IStorageService storageService)
    {
        _storageService = storageService;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(CompleteSkillFileCommand request, CancellationToken cancellationToken)
    {
        await _storageService.CompleteAsync(request.FileId, request.IsSuccess, cancellationToken);
        return EmptyCommandResponse.Default;
    }
}
