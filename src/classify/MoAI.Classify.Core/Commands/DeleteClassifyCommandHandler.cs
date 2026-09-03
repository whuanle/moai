using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.Classify.Commands;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.Classify.Handlers;

/// <summary>
/// <inheritdoc cref="DeleteClassifyCommand"/>
/// </summary>
public class DeleteClassifyCommandHandler : IRequestHandler<DeleteClassifyCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteClassifyCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public DeleteClassifyCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(DeleteClassifyCommand request, CancellationToken cancellationToken)
    {
        var classify = await _databaseContext.Classifies
            .FirstOrDefaultAsync(x => x.Id == request.ClassifyId && x.IsDeleted == 0, cancellationToken);

        if (classify == null)
        {
            throw new BusinessException("未找到分类，请检查 id 是否正确.") { StatusCode = 404 };
        }

        // 引用校验：不同类型引用表不同. 当前插件类型校验 Plugins.ClassifyId，
        // 应用/知识库业务尚未实现，暂无引用表，删除时可删（预留扩展点）.
        if (classify.Type == ClassifyTypes.Plugin)
        {
            var used = await _databaseContext.Plugins
                .AnyAsync(x => x.ClassifyId == request.ClassifyId && x.IsDeleted == 0, cancellationToken);

            if (used)
            {
                throw new BusinessException("该分类下仍存在插件，无法删除.") { StatusCode = 409 };
            }
        }

        classify.IsDeleted = 1;
        _databaseContext.Update(classify);
        await _databaseContext.SaveChangesAsync(cancellationToken);

        return EmptyCommandResponse.Default;
    }
}
