using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AI.Models;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Wiki.Wikis.Commands;

namespace MoAI.Wiki.Wikis.Handlers;

/// <summary>
/// 更新知识库配置.
/// </summary>
public class UpdateWikiConfigCommandHandler : IRequestHandler<UpdateWikiConfigCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateWikiConfigCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext"></param>
    public UpdateWikiConfigCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(UpdateWikiConfigCommand request, CancellationToken cancellationToken)
    {
        var wikiEntity = await _databaseContext.Wikis.Where(x => x.Id == request.WikiId).FirstOrDefaultAsync();

        if (wikiEntity == null)
        {
            throw new BusinessException("知识库不存在") { StatusCode = 500 };
        }

        if (wikiEntity.Name != request.Name)
        {
            var exist = await _databaseContext.Wikis.AnyAsync(x => x.Id != wikiEntity.Id && x.Name == request.Name, cancellationToken);
            if (exist)
            {
                throw new BusinessException("知识库名称重复") { StatusCode = 409 };
            }
        }

        if (request.EmbeddingDimensions != 0 && request.EmbeddingModelId != 0)
        {
            if (wikiEntity.IsLock)
            {
                throw new BusinessException("知识库已锁定配置，禁止更新") { StatusCode = 409 };
            }

            // 检查模型是否存在
            var existAiModel = await _databaseContext.AiModels.AnyAsync(x => x.IsPublic && x.Id == request.EmbeddingModelId && x.AiModelType == AiModelType.Embedding.ToJsonString());
            if (!existAiModel)
            {
                throw new BusinessException("模型不存在或非向量化模型");
            }

            wikiEntity.EmbeddingDimensions = request.EmbeddingDimensions;
            wikiEntity.EmbeddingModelId = request.EmbeddingModelId;
        }

        // 私有知识库不能改成公开知识库
        if (wikiEntity.TeamId != 0)
        {
            wikiEntity.IsPublic = request.IsPublic;
        }

        wikiEntity.Name = request.Name;
        wikiEntity.Description = request.Description;

        if (!string.IsNullOrEmpty(request.Avatar))
        {
            wikiEntity.Avatar = request.Avatar;
        }

        _databaseContext.Wikis.Update(wikiEntity);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
