using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Database.Enums;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;
using MoAI.Infra.Services;
using MoAI.Team.Services;
using MoAI.Wiki.Commands;
using MoAI.Wiki.Services;

namespace MoAI.Wiki.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteWikiCommand"/>
/// </summary>
public class DeleteWikiCommandHandler : IRequestHandler<DeleteWikiCommand, EmptyCommandResponse>
{
    /// <summary>
    /// 级联删除文档的分批大小（控制单次载入内存的文档/切片实体规模）.
    /// </summary>
    private const int RemovalBatchSize = 100;

    private readonly DatabaseContext _databaseContext;
    private readonly ITeamService _teamService;
    private readonly IUserContextProvider _userContextProvider;
    private readonly WikiDocumentRemovalService _removalService;
    private readonly IWikiEmbeddingVectorStore _vectorStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteWikiCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    /// <param name="teamService">团队领域服务.</param>
    /// <param name="userContextProvider">用户上下文提供者.</param>
    /// <param name="removalService">文档删除领域服务（向量/切片/元数据/正文/映射/OSS/图片全链路清理）.</param>
    /// <param name="vectorStore">知识库向量存储.</param>
    public DeleteWikiCommandHandler(DatabaseContext databaseContext, ITeamService teamService, IUserContextProvider userContextProvider, WikiDocumentRemovalService removalService, IWikiEmbeddingVectorStore vectorStore)
    {
        _databaseContext = databaseContext;
        _teamService = teamService;
        _userContextProvider = userContextProvider;
        _removalService = removalService;
        _vectorStore = vectorStore;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteWikiCommand request, CancellationToken cancellationToken)
    {
        var wiki = await _databaseContext.Wikis
            .FirstOrDefaultAsync(x => x.Id == request.WikiId, cancellationToken);

        if (wiki == null)
        {
            throw new BusinessException("知识库不存在.") { StatusCode = 404 };
        }

        var userId = _userContextProvider.GetUserContext().UserId;
        var myRole = await _teamService.GetMyRoleAsync(wiki.TeamId, userId, cancellationToken);

        if (myRole == null)
        {
            throw new BusinessException("团队不存在或你不是团队成员.") { StatusCode = 404 };
        }

        if (myRole == TeamRole.Member)
        {
            throw new BusinessException("只有团队管理员可以删除知识库.") { StatusCode = 403 };
        }

        // 级联删除该库全部文档（含向量/OSS/图片等关联数据），分批控制内存
        var documentIds = await _databaseContext.WikiDocuments
            .Where(x => x.WikiId == wiki.Id && x.IsDeleted == 0)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        foreach (var batchStart in Enumerable.Range(0, (documentIds.Count + RemovalBatchSize - 1) / RemovalBatchSize))
        {
            var idBatch = documentIds.Skip(batchStart * RemovalBatchSize).Take(RemovalBatchSize).ToList();
            if (idBatch.Count == 0)
            {
                continue;
            }

            var documents = await _databaseContext.WikiDocuments
                .Where(x => idBatch.Contains(x.Id))
                .ToArrayAsync(cancellationToken);
            await _removalService.RemoveDocumentsAsync(wiki.Id, documents, cancellationToken);
        }

        // 整库向量集合一次性 DROP（兜底清理历史孤儿向量与未随文档删除的残留）
        await _vectorStore.DeleteWikiVectorsAsync(wiki.Id, cancellationToken);

        // 实体实现 IFullAudited，框架将删除转换为软删除并记录操作人
        _databaseContext.Wikis.Remove(wiki);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
