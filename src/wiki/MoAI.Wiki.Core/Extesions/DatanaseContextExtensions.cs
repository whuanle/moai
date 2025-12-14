using Microsoft.EntityFrameworkCore;
using MoAI.AI.Models;
using MoAI.Database;
using MoAI.Infra.Extensions;

namespace MoAI.Wiki.Extesions;

internal static class DatanaseContextExtensions
{
    public static async Task<AiEndpoint> QueryWikiAiModelAsync(this DatabaseContext databaseContext, int wikiId)
    {
        var aiModel = await databaseContext.Wikis
        .Where(x => x.Id == wikiId)
        .Join(databaseContext.AiModels, a => a.EmbeddingModelId, b => b.Id, (a, x) => new AiEndpoint
        {
            Name = x.Name,
            DeploymentName = x.DeploymentName,
            Title = x.Title,
            AiModelType = x.AiModelType.JsonToObject<AiModelType>(),
            Provider = x.AiProvider.JsonToObject<AiProvider>(),
            ContextWindowTokens = x.ContextWindowTokens,
            Endpoint = x.Endpoint,
            Abilities = new ModelAbilities
            {
                Files = x.Files,
                FunctionCall = x.FunctionCall,
                ImageOutput = x.ImageOutput,
                Vision = x.IsVision,
            },
            MaxDimension = x.MaxDimension,
            TextOutput = x.TextOutput,
            Key = x.Key,
        }).FirstOrDefaultAsync();

        return aiModel;
    }
}
