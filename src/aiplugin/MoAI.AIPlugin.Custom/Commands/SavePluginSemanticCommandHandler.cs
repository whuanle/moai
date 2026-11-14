using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MoAI.AIPlugin.Commands;
using MoAI.Database;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Models;

namespace MoAI.AIPlugin.Handlers;

/// <summary>
/// <inheritdoc cref="SavePluginSemanticCommand"/> 整体覆盖保存插件语义配置.
/// </summary>
public class SavePluginSemanticCommandHandler : IRequestHandler<SavePluginSemanticCommand, EmptyCommandResponse>
{
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="SavePluginSemanticCommandHandler"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public SavePluginSemanticCommandHandler(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task<EmptyCommandResponse> Handle(SavePluginSemanticCommand request, CancellationToken cancellationToken)
    {
        var plugin = await _databaseContext.Plugins
            .FirstOrDefaultAsync(x => x.Id == request.PluginId, cancellationToken)
            ?? throw new BusinessException("插件不存在") { StatusCode = 404 };

        plugin.UsageNotes = request.UsageNotes;
        _databaseContext.Plugins.Update(plugin);

        await SaveNotesAsync(request, cancellationToken);
        await SaveToolMetasAsync(request, cancellationToken);

        await _databaseContext.SaveChangesAsync(cancellationToken);
        return EmptyCommandResponse.Default;
    }

    private async Task SaveNotesAsync(SavePluginSemanticCommand request, CancellationToken cancellationToken)
    {
        var existing = await _databaseContext.PluginSemanticNotes
            .Where(x => x.PluginId == request.PluginId)
            .ToListAsync(cancellationToken);

        var submittedIds = request.Notes.Where(x => x.Id.HasValue).Select(x => x.Id!.Value).ToHashSet();

        // 整体覆盖：不在提交列表内的既有条目软删除
        foreach (var row in existing.Where(x => !submittedIds.Contains(x.Id)))
        {
            row.IsDeleted = 1;
            _databaseContext.PluginSemanticNotes.Update(row);
        }

        foreach (var item in request.Notes)
        {
            var mode = string.Equals(item.Mode, "onDemand", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            var row = item.Id.HasValue
                ? existing.FirstOrDefault(x => x.Id == item.Id!.Value)
                : null;

            if (row != null)
            {
                row.ToolKey = item.ToolKey;
                row.Title = item.Title;
                row.Content = item.Content;
                row.Mode = mode;
                row.SortOrder = item.SortOrder;
                _databaseContext.PluginSemanticNotes.Update(row);
            }
            else
            {
                _databaseContext.PluginSemanticNotes.Add(new Database.Entities.PluginSemanticNoteEntity
                {
                    PluginId = request.PluginId,
                    ToolKey = item.ToolKey,
                    Title = item.Title,
                    Content = item.Content,
                    Mode = mode,
                    SortOrder = item.SortOrder,
                });
            }
        }
    }

    private async Task SaveToolMetasAsync(SavePluginSemanticCommand request, CancellationToken cancellationToken)
    {
        var existing = await _databaseContext.PluginToolMetas
            .Where(x => x.PluginId == request.PluginId)
            .ToListAsync(cancellationToken);

        var submittedKeys = request.ToolMetas.Select(x => x.ToolKey).ToHashSet(StringComparer.Ordinal);

        // 整体覆盖：不在提交列表内的既有元数据软删除
        foreach (var row in existing.Where(x => !submittedKeys.Contains(x.ToolKey)))
        {
            row.IsDeleted = 1;
            _databaseContext.PluginToolMetas.Update(row);
        }

        foreach (var item in request.ToolMetas)
        {
            var row = existing.FirstOrDefault(x => x.ToolKey == item.ToolKey);
            if (row != null)
            {
                row.ExtraNotes = item.ExtraNotes;
                row.CallPolicy = item.CallPolicy;
                row.ReadOnlyHint = item.ReadOnlyHint;
                row.DestructiveHint = item.DestructiveHint;
                _databaseContext.PluginToolMetas.Update(row);
            }
            else
            {
                _databaseContext.PluginToolMetas.Add(new Database.Entities.PluginToolMetaEntity
                {
                    PluginId = request.PluginId,
                    ToolKey = item.ToolKey,
                    ExtraNotes = item.ExtraNotes,
                    CallPolicy = item.CallPolicy,
                    ReadOnlyHint = item.ReadOnlyHint,
                    DestructiveHint = item.DestructiveHint,
                });
            }
        }
    }
}
