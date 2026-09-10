using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Anthropic;
using Anthropic.Models.Messages;
using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.AI;
using MoAI.AIChannel.Models;
using MoAI.Database.Entities;
using MoAI.Infra.Exceptions;
using OpenAI;

namespace MoAI.AIChannel.Services;

/// <summary>
/// 一次性对话服务实现.
/// </summary>
public class AIChatCompletionService : IAiChatCompletionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IChatClientProvider _chatClientProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIChatCompletionService"/> class.
    /// </summary>
    /// <param name="chatClientProvider">对话客户端提供者.</param>
    public AIChatCompletionService(IChatClientProvider chatClientProvider)
    {
        _chatClientProvider = chatClientProvider;
    }

    /// <inheritdoc/>
    public async Task<string> CompleteTextAsync(AiModelEntity model, AiChannelEntity channel, string prompt, AiChatCompletionOptions options, CancellationToken cancellationToken = default)
    {
        var protocol = (AIProtocolFamily)channel.ProtocolFamily;
        return protocol switch
        {
            AIProtocolFamily.OpenAIChatCompletions or AIProtocolFamily.OpenAIResponses
                => options.DisableThinking
                    ? await CompleteOpenAICompatibleTextWithNativeClientAsync(model, channel, prompt, options, cancellationToken)
                    : await CompleteWithIChatClientAsync(model, channel, prompt, options, cancellationToken),
            AIProtocolFamily.AnthropicMessages
                => await CompleteAnthropicTextAsync(model, channel, prompt, options, cancellationToken),
            AIProtocolFamily.GoogleGemini
                => await CompleteGeminiTextAsync(model, channel, prompt, options, cancellationToken),
            _ => await CompleteWithIChatClientAsync(model, channel, prompt, options, cancellationToken),
        };
    }

    private async Task<string> CompleteWithIChatClientAsync(AiModelEntity model, AiChannelEntity channel, string prompt, AiChatCompletionOptions options, CancellationToken cancellationToken)
    {
        var chatClient = await _chatClientProvider.GetChatClientAsync(model, channel, cancellationToken);
        var chatOptions = new ChatOptions
        {
            Temperature = options.Temperature,
            MaxOutputTokens = options.MaxOutputTokens,
        };
        var response = await chatClient.GetResponseAsync(prompt, chatOptions, cancellationToken);
        return response.Text ?? string.Empty;
    }

    private static async Task<string> CompleteAnthropicTextAsync(AiModelEntity model, AiChannelEntity channel, string prompt, AiChatCompletionOptions options, CancellationToken cancellationToken)
    {
        var clientOptions = new Anthropic.Core.ClientOptions
        {
            ApiKey = channel.ApiKey,
        };
        if (!string.IsNullOrWhiteSpace(channel.BaseUrl))
        {
            clientOptions.BaseUrl = channel.BaseUrl.TrimEnd('/');
        }

        using var client = new AnthropicClient(clientOptions);
        var parameters = new MessageCreateParams
        {
            Model = model.ModelId,
            MaxTokens = options.MaxOutputTokens ?? 512,
            Thinking = options.DisableThinking ? new ThinkingConfigDisabled() : null!,
            Messages =
            [
                new MessageParam
                {
                    Role = Role.User,
                    Content = prompt,
                },
            ],
        };
        var response = await client.Messages.Create(parameters, cancellationToken);

        var parts = new List<string>();
        foreach (var block in response.Content)
        {
            if (block.TryPickText(out var textBlock) && !string.IsNullOrWhiteSpace(textBlock.Text))
            {
                parts.Add(textBlock.Text);
            }
        }

        return string.Concat(parts);
    }

    private static async Task<string> CompleteGeminiTextAsync(AiModelEntity model, AiChannelEntity channel, string prompt, AiChatCompletionOptions options, CancellationToken cancellationToken)
    {
        var httpOptions = string.IsNullOrWhiteSpace(channel.BaseUrl)
            ? null
            : new HttpOptions { BaseUrl = channel.BaseUrl.TrimEnd('/') };
        using var client = new Client(apiKey: channel.ApiKey, httpOptions: httpOptions);
        var response = await client.Models.GenerateContentAsync(
            model: model.ModelId,
            contents: prompt,
            config: new GenerateContentConfig
            {
                Temperature = options.Temperature,
                MaxOutputTokens = options.MaxOutputTokens,
                ResponseMimeType = options.PreferJsonResponse ? "application/json" : null,
                ThinkingConfig = options.DisableThinking
                    ? new ThinkingConfig
                    {
                        IncludeThoughts = false,
                        ThinkingBudget = 0,
                    }
                    : null,
            },
            cancellationToken: cancellationToken);
        return response.Text ?? string.Empty;
    }

    private static async Task<string> CompleteOpenAICompatibleTextWithNativeClientAsync(AiModelEntity model, AiChannelEntity channel, string prompt, AiChatCompletionOptions options, CancellationToken cancellationToken)
    {
        var request = new OpenAIChatCompletionRequest
        {
            Model = model.ModelId,
            Messages = [new OpenAIChatMessage("user", prompt)],
            Temperature = options.Temperature,
            MaxTokens = options.MaxOutputTokens,
            ResponseFormat = options.PreferJsonResponse ? new OpenAIResponseFormat("text") : null,
            ReasoningEffort = "none",
            EnableThinking = false,
            ChatTemplateKwargs = new Dictionary<string, object?>
            {
                ["enable_thinking"] = false,
            },
        };

        var client = CreateOpenAIClient(channel).GetChatClient(model.ModelId);
        using var content = BinaryContent.Create(BinaryData.FromObjectAsJson(request, JsonOptions));
        var requestOptions = new RequestOptions { CancellationToken = cancellationToken };
        ClientResult response = await client.CompleteChatAsync(content, requestOptions);
        var responseText = response.GetRawResponse().Content.ToString();
        return ExtractOpenAIContent(responseText) ?? string.Empty;
    }

    private static OpenAIClient CreateOpenAIClient(AiChannelEntity channel)
    {
        var credential = new ApiKeyCredential(channel.ApiKey);
        if (string.IsNullOrWhiteSpace(channel.BaseUrl))
        {
            return new OpenAIClient(credential);
        }

        return new OpenAIClient(credential, new OpenAIClientOptions
        {
            Endpoint = new Uri(channel.BaseUrl.TrimEnd('/')),
        });
    }

    private static string? ExtractOpenAIContent(string responseText)
    {
        using var document = JsonDocument.Parse(responseText);
        var root = document.RootElement;
        if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
        {
            var first = choices[0];
            if (first.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var content))
            {
                return content.GetString();
            }
        }

        if (root.TryGetProperty("output_text", out var outputText))
        {
            return outputText.GetString();
        }

        return null;
    }

    private sealed record OpenAIChatCompletionRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; init; } = string.Empty;

        [JsonPropertyName("messages")]
        public List<OpenAIChatMessage> Messages { get; init; } = new();

        [JsonPropertyName("temperature")]
        public float? Temperature { get; init; }

        [JsonPropertyName("max_tokens")]
        public int? MaxTokens { get; init; }

        [JsonPropertyName("response_format")]
        public OpenAIResponseFormat? ResponseFormat { get; init; }

        [JsonPropertyName("reasoning_effort")]
        public string ReasoningEffort { get; init; } = "none";

        [JsonPropertyName("enable_thinking")]
        public bool EnableThinking { get; init; }

        [JsonPropertyName("chat_template_kwargs")]
        public Dictionary<string, object?> ChatTemplateKwargs { get; init; } = new();
    }

    private sealed record OpenAIChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record OpenAIResponseFormat([property: JsonPropertyName("type")] string Type);
}