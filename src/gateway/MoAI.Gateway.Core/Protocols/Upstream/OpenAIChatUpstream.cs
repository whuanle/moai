using System.Text.Json;
using System.Text.Json.Nodes;
using MoAI.AIChannel.Models;
using MoAI.Database.Entities;

namespace MoAI.Gateway.Protocols.Upstream;

/// <summary>
/// OpenAI Chat Completions 上游协议.
/// </summary>
public class OpenAIChatUpstream : IUpstreamProtocol
{
    /// <inheritdoc/>
    public AIProtocolFamily Family => AIProtocolFamily.OpenAIChatCompletions;

    /// <inheritdoc/>
    public HttpRequestMessage BuildRequest(string baseUrl, string apiKey, AiModelEntity model, GatewayChatRequest request)
    {
        var url = UpstreamHelper.NormalizeOpenAiBase(baseUrl) + "/chat/completions";
        var body = new JsonObject
        {
            ["model"] = model.ModelId,
            ["messages"] = BuildMessages(request),
            ["stream"] = request.Stream,
        };

        if (request.Stream)
        {
            body["stream_options"] = new JsonObject { ["include_usage"] = true };
        }

        if (request.Tools != null)
        {
            var tools = new JsonArray();
            foreach (var tool in request.Tools)
            {
                tools.Add(new JsonObject
                {
                    ["type"] = "function",
                    ["function"] = new JsonObject
                    {
                        ["name"] = tool.Name,
                        ["description"] = tool.Description,
                        ["parameters"] = UpstreamHelper.SchemaToNode(tool.ParametersSchema),
                    },
                });
            }

            body["tools"] = tools;
            if (request.ToolChoice != null)
            {
                body["tool_choice"] = request.ToolChoice.Mode switch
                {
                    GatewayToolChoiceMode.None => "none",
                    GatewayToolChoiceMode.Required => "required",
                    GatewayToolChoiceMode.Function => new JsonObject
                    {
                        ["type"] = "function",
                        ["function"] = new JsonObject { ["name"] = request.ToolChoice.FunctionName },
                    },
                    _ => "auto",
                };
            }
        }

        if (request.Temperature.HasValue)
        {
            body["temperature"] = request.Temperature.Value;
        }

        if (request.TopP.HasValue)
        {
            body["top_p"] = request.TopP.Value;
        }

        if (request.MaxOutputTokens.HasValue)
        {
            body["max_tokens"] = request.MaxOutputTokens.Value;
        }

        if (request.StopSequences is { Count: > 0 })
        {
            body["stop"] = new JsonArray(request.StopSequences.Select(x => JsonValue.Create(x)).ToArray());
        }

        if (!string.IsNullOrEmpty(request.User))
        {
            body["user"] = request.User;
        }

        var message = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json"),
        };
        message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        return message;
    }

    /// <inheritdoc/>
    public GatewayChatResponse ParseResponse(string body)
    {
        var root = UpstreamHelper.Parse(body);
        var response = new GatewayChatResponse
        {
            Id = GatewayJsonReader.String(root, "id") ?? GatewayIds.New("chatcmpl-"),
            Model = GatewayJsonReader.String(root, "model") ?? string.Empty,
        };

        var choice = GatewayJsonReader.First(GatewayJsonReader.Array(root, "choices"));
        if (choice.ValueKind == JsonValueKind.Object)
        {
            if (GatewayJsonReader.Object(choice, "message") is { } message)
            {
                response.Text = GatewayJsonReader.String(message, "content");
                var toolCalls = GatewayJsonReader.Array(message, "tool_calls");
                if (toolCalls != null)
                {
                    foreach (var toolCall in toolCalls.Value.EnumerateArray())
                    {
                        if (GatewayJsonReader.Object(toolCall, "function") is not { } function)
                        {
                            continue;
                        }

                        response.ToolCalls.Add(new GatewayToolCall(
                            GatewayJsonReader.String(toolCall, "id") ?? GatewayIds.New("call_"),
                            GatewayJsonReader.String(function, "name") ?? string.Empty,
                            GatewayJsonReader.String(function, "arguments") ?? "{}"));
                    }
                }
            }

            response.FinishReason = GatewayJsonReader.String(choice, "finish_reason") switch
            {
                "length" => GatewayFinishReason.Length,
                "tool_calls" or "function_call" => GatewayFinishReason.ToolCalls,
                "content_filter" => GatewayFinishReason.ContentFilter,
                _ => GatewayFinishReason.Stop,
            };
        }

        if (GatewayJsonReader.Object(root, "usage") is { } usage)
        {
            response.Usage = new GatewayUsage(
                GatewayJsonReader.Int(usage, "prompt_tokens") ?? 0,
                GatewayJsonReader.Int(usage, "completion_tokens") ?? 0);
        }

        return response;
    }

    /// <inheritdoc/>
    public IUpstreamSseParser CreateSseParser(string model)
    {
        return new ChatSseParser();
    }

    private static JsonArray BuildMessages(GatewayChatRequest request)
    {
        var messages = new JsonArray();
        foreach (var message in request.Messages)
        {
            var obj = new JsonObject { ["role"] = message.Role switch { GatewayRole.System => "system", GatewayRole.Assistant => "assistant", GatewayRole.Tool => "tool", _ => "user" } };

            if (message.Parts != null && message.Parts.OfType<GatewayImagePart>().Any())
            {
                var parts = new JsonArray();
                var hasText = false;
                foreach (var part in message.Parts)
                {
                    if (part is GatewayTextPart textPart)
                    {
                        parts.Add(new JsonObject { ["type"] = "text", ["text"] = textPart.Text });
                        hasText = true;
                    }
                    else if (part is GatewayImagePart imagePart)
                    {
                        var image = UpstreamHelper.ImageToOpenAiPart(imagePart);
                        if (image != null)
                        {
                            parts.Add(image);
                        }
                    }
                }

                obj["content"] = parts;
                if (!hasText)
                {
                    obj["content"] = parts;
                }
            }
            else
            {
                obj["content"] = message.Text ?? string.Empty;
            }

            if (message.ToolCalls is { Count: > 0 })
            {
                var toolCalls = new JsonArray();
                foreach (var toolCall in message.ToolCalls)
                {
                    toolCalls.Add(new JsonObject
                    {
                        ["id"] = toolCall.Id,
                        ["type"] = "function",
                        ["function"] = new JsonObject
                        {
                            ["name"] = toolCall.Name,
                            ["arguments"] = toolCall.ArgumentsJson,
                        },
                    });
                }

                obj["tool_calls"] = toolCalls;
            }

            if (message.Role == GatewayRole.Tool)
            {
                obj["tool_call_id"] = message.ToolCallId ?? string.Empty;
                if (!string.IsNullOrEmpty(message.Name))
                {
                    obj["name"] = message.Name;
                }
            }

            messages.Add(obj);
        }

        return messages;
    }

    private sealed class ChatSseParser : IUpstreamSseParser
    {
        private GatewayUsage? _usage;
        private GatewayFinishReason _finishReason = GatewayFinishReason.Stop;
        private bool _started;

        public IEnumerable<GatewayStreamEvent> Feed(string data)
        {
            var trimmed = data.Trim();
            if (trimmed == "[DONE]")
            {
                yield return new GatewayStreamEvent.Finished(_finishReason, _usage);
                yield break;
            }

            JsonElement root;
            try
            {
                root = JsonDocument.Parse(trimmed).RootElement.Clone();
            }
            catch (JsonException)
            {
                yield break;
            }

            if (!_started)
            {
                _started = true;
                yield return new GatewayStreamEvent.Started(
                    GatewayJsonReader.String(root, "id") ?? GatewayIds.New("chatcmpl-"),
                    GatewayJsonReader.String(root, "model") ?? string.Empty);
            }

            if (GatewayJsonReader.Object(root, "usage") is { } usage)
            {
                _usage = new GatewayUsage(
                    GatewayJsonReader.Int(usage, "prompt_tokens") ?? 0,
                    GatewayJsonReader.Int(usage, "completion_tokens") ?? 0);
            }

            var choice = GatewayJsonReader.First(GatewayJsonReader.Array(root, "choices"));
            if (choice.ValueKind != JsonValueKind.Object)
            {
                yield break;
            }

            if (GatewayJsonReader.Object(choice, "delta") is { } delta)
            {
                var content = GatewayJsonReader.String(delta, "content");
                if (!string.IsNullOrEmpty(content))
                {
                    yield return new GatewayStreamEvent.TextDelta(content);
                }

                var toolCalls = GatewayJsonReader.Array(delta, "tool_calls");
                if (toolCalls != null)
                {
                    foreach (var toolCall in toolCalls.Value.EnumerateArray())
                    {
                        var index = GatewayJsonReader.Int(toolCall, "index") ?? 0;
                        if (GatewayJsonReader.Object(toolCall, "function") is { } function)
                        {
                            var name = GatewayJsonReader.String(function, "name");
                            if (!string.IsNullOrEmpty(name))
                            {
                                var id = GatewayJsonReader.String(toolCall, "id") ?? GatewayIds.New("call_");
                                yield return new GatewayStreamEvent.ToolCallStarted(index, id, name);
                            }

                            var args = GatewayJsonReader.String(function, "arguments");
                            if (!string.IsNullOrEmpty(args))
                            {
                                yield return new GatewayStreamEvent.ToolCallArgumentsDelta(index, args);
                            }
                        }
                    }
                }
            }

            var finish = GatewayJsonReader.String(choice, "finish_reason");
            if (!string.IsNullOrEmpty(finish))
            {
                _finishReason = finish switch
                {
                    "length" => GatewayFinishReason.Length,
                    "tool_calls" or "function_call" => GatewayFinishReason.ToolCalls,
                    "content_filter" => GatewayFinishReason.ContentFilter,
                    _ => GatewayFinishReason.Stop,
                };
            }
        }
    }
}
