#pragma warning disable SKEXP0001 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable SKEXP0040 // 类型仅用于评估，在将来的更新中可能会被更改或删除。取消此诊断以继续。
#pragma warning disable CA1849 // 当在异步方法中时，调用异步方法
#pragma warning disable CA1031 // 不捕获常规异常类型

using Maomi.MQ;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using MoAI.AI.Chat;
using MoAI.AI.Chat.Models;
using MoAI.AI.ChatCompletion;
using MoAI.AI.Models;
using MoAI.App.Chat.Works.Models;
using MoAI.Database;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using MoAI.Infra.Extensions;
using MoAI.Infra.Models;
using MoAI.Plugin;
using StackExchange.Redis.Extensions.Core.Abstractions;
using System.Runtime.CompilerServices;

namespace MoAI.App.Chat.Works.Commands;

public class ClearDebugChatAppInstanceCommandHandler : IRequestHandler<ClearDebugChatAppInstanceCommand, EmptyCommandResponse>
{
    private readonly IRedisDatabase _redisDatabase;

    public ClearDebugChatAppInstanceCommandHandler(IRedisDatabase redisDatabase)
    {
        _redisDatabase = redisDatabase;
    }

    public async Task<EmptyCommandResponse> Handle(ClearDebugChatAppInstanceCommand request, CancellationToken cancellationToken)
    {
        var debugKey = $"{request.AppId}:{request.ContextUserId}";
        await _redisDatabase.RemoveAsync(debugKey);

        return EmptyCommandResponse.Default;
    }
}
