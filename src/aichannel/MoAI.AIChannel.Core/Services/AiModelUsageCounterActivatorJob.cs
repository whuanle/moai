using Microsoft.EntityFrameworkCore;
using MoAI.Database;
using MoAI.Hangfire.Services;

namespace MoAI.AIChannel.Services;

/// <summary>
/// 将 Redis 中的 AI 模型用量增量刷新到 PostgreSQL.
/// </summary>
public class AiModelUsageCounterActivatorJob : ICounterActivatorJob
{
    private const string CounterName = "ai-model-usage";
    private readonly DatabaseContext _databaseContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="AiModelUsageCounterActivatorJob"/> class.
    /// </summary>
    /// <param name="databaseContext">数据库上下文.</param>
    public AiModelUsageCounterActivatorJob(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <inheritdoc/>
    public async Task ActivateAsync(IReadOnlyDictionary<string, long> values)
    {
        var increments = Aggregate(values);
        if (increments.Count == 0)
        {
            return;
        }

        await using var transaction = await _databaseContext.Database.BeginTransactionAsync();
        foreach (var item in increments)
        {
            await UpsertAsync(item.Key, item.Value);
        }

        await transaction.CommitAsync();
    }

    /// <inheritdoc/>
    public Task<string> GetNameAsync()
    {
        return Task.FromResult(CounterName);
    }

    internal static IReadOnlyDictionary<AiModelUsageCounterDimension, UsageIncrement> Aggregate(IReadOnlyDictionary<string, long> values)
    {
        Dictionary<AiModelUsageCounterDimension, UsageIncrement> increments = new();
        foreach (var item in values)
        {
            if (item.Value <= 0 || !AiModelUsageCounterKey.TryParse(item.Key, out var dimension, out var metric))
            {
                continue;
            }

            if (!increments.TryGetValue(dimension, out var increment))
            {
                increment = new UsageIncrement();
                increments.Add(dimension, increment);
            }

            increment.Add(metric, item.Value);
        }

        return increments;
    }

    private Task<int> UpsertAsync(AiModelUsageCounterDimension dimension, UsageIncrement increment)
    {
        return _databaseContext.Database.ExecuteSqlInterpolatedAsync($$"""
            INSERT INTO ai_model_token_audit
                (model_id, team_id, user_id, use_type, use_resource_id,
                 completion_tokens, prompt_tokens, total_tokens, count,
                 create_user_id, create_time, update_user_id, update_time, is_deleted)
            SELECT {{dimension.ModelId}}, {{dimension.TeamId}}, {{dimension.UserId}}, {{dimension.UseType}}, {{dimension.UseResourceId}},
                   {{increment.CompletionTokens}}, {{increment.PromptTokens}}, {{increment.TotalTokens}}, {{increment.Count}},
                   {{dimension.UserId}}, timezone('utc', now()), {{dimension.UserId}}, timezone('utc', now()), 0
            WHERE EXISTS (
                SELECT 1 FROM ai_model
                WHERE id = {{dimension.ModelId}} AND is_deleted = 0)
            ON CONFLICT (model_id, team_id, user_id, use_type, use_resource_id)
                WHERE is_deleted = 0
            DO UPDATE SET
                completion_tokens = ai_model_token_audit.completion_tokens + EXCLUDED.completion_tokens,
                prompt_tokens = ai_model_token_audit.prompt_tokens + EXCLUDED.prompt_tokens,
                total_tokens = ai_model_token_audit.total_tokens + EXCLUDED.total_tokens,
                count = ai_model_token_audit.count + EXCLUDED.count,
                update_user_id = EXCLUDED.update_user_id,
                update_time = EXCLUDED.update_time;
            """);
    }

    internal sealed class UsageIncrement
    {
        public long CompletionTokens { get; private set; }

        public long PromptTokens { get; private set; }

        public long TotalTokens { get; private set; }

        public long Count { get; private set; }

        public void Add(AiModelUsageMetric metric, long value)
        {
            switch (metric)
            {
                case AiModelUsageMetric.Count:
                    Count += value;
                    break;
                case AiModelUsageMetric.Prompt:
                    PromptTokens += value;
                    break;
                case AiModelUsageMetric.Completion:
                    CompletionTokens += value;
                    break;
                case AiModelUsageMetric.Total:
                    TotalTokens += value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(metric));
            }
        }
    }
}