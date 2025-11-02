using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.User.Queries;
using MoAIPrompt.Models;
using MoAIPrompt.Queries;
using MoAIPrompt.Queries.Responses;

namespace MoAI.Prompt.Queries;

/// <summary>
/// <inheritdoc cref="QueryPromptListCommand"/>
/// </summary>
public class QueryPromptListCommandHandler : IRequestHandler<QueryPromptListCommand, QueryPromptListCommandResponse>
{
    private readonly DatabaseContext _databaseContext;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueryPromptListCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    /// <param name="mediator"></param>
    public QueryPromptListCommandHandler(DatabaseContext databaseContext, IMediator mediator)
    {
        _databaseContext = databaseContext;
        _mediator = mediator;
    }

    /// <inheritdoc/>
    public async Task<QueryPromptListCommandResponse> Handle(QueryPromptListCommand request, CancellationToken cancellationToken)
    {
        var query = _databaseContext.Prompts.AsQueryable();

        if (request.Condition == PromptFilterCondition.Own)
        {
            query = query.Where(x => x.CreateUserId == request.ContextUserId);
        }
        else if (request.Condition == PromptFilterCondition.OwnPublic)
        {
            query = query.Where(x => x.CreateUserId == request.ContextUserId && x.IsPublic);
        }
        else if (request.Condition == PromptFilterCondition.OwnPrivate)
        {
            query = query.Where(x => x.CreateUserId == request.ContextUserId && x.IsPublic == false);
        }
        else if (request.Condition == PromptFilterCondition.OtherShare)
        {
            query = query.Where(x => x.CreateUserId != request.ContextUserId && x.IsPublic == true);
        }
        else
        {
            query = query.Where(x => x.CreateUserId == request.ContextUserId || (x.CreateUserId != request.ContextUserId && x.IsPublic == true));
        }

        if (request.ClassId != null)
        {
            query = query.Where(x => x.PromptClassId == request.ClassId);
        }

        if (!string.IsNullOrEmpty(request.Search))
        {
            query = query.Where(x => x.Name.Contains(request.Search));
        }

        var prompts = await query
            .Select(x => new PromptItem
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                CreateTime = x.CreateTime,
                UpdateTime = x.UpdateTime,
                CreateUserId = x.CreateUserId,
                UpdateUserId = x.UpdateUserId,
                IsPublic = x.IsPublic,
                Content = string.Empty,
                PromptClassId = x.CreateUserId
            })
            .OrderByDescending(x => x.Name)
            .Skip(request.Skip)
            .Take(request.Take)
            .ToArrayAsync(cancellationToken);

        await _mediator.Send(new FillUserInfoCommand
        {
            Items = prompts
        });

        return new QueryPromptListCommandResponse
        {
            Items = prompts
        };
    }
}
