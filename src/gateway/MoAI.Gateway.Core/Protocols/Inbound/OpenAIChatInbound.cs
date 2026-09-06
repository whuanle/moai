using System.Text.Json;
using System.Text.Json.Nodes;

namespace MoAI.Gateway.Protocols.Inbound;

/// <summary>
/// OpenAI Chat Completions 入口协议（/v1/chat/completions）.
/// </summary>
public class OpenAIChatInbound : IInboundProtocol
{
    /// <inheritdoc/>
    public GatewayInboundFormat Format => GatewayInboundFormat.OpenAIChatCompletions;

    /// <inheritdoc/>
    public string IdPrefix => "chatcmpl-";

    /// <inheritdoc/>
    public GatewayChatRequest ParseRequest(JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Object)
        {
            throw new GatewayProtocolException(400, "Request body must be a JSON object.", "invalid_request_error");
        }

        var model = GatewayJsonReader.String(body, "model");
        if (string.IsNullOrEmpty(model))
        {
            throw new GatewayProtocolException(400, "'model' is required.", "invalid_request_error");
        }

        var request = new GatewayChatRequest
        {
            Model = model,
            Stream = GatewayJsonReader.Bool(body, "stream") == true,
            Temperature = GatewayJsonReader.Double(body, "temperature"),
            TopP = GatewayJsonReader.Double(body, "top_p"),
            MaxOutputTokens = GatewayJsonReader.Int(body, "max_tokens") ?? GatewayJsonReader.Int(body, "max_completion_tokens"),
            User = GatewayJsonReader.String(body, "user"),
        };

        ParseStop(body, request);
        ParseMessages(body, request);
        ParseTools(body, request);
        return request;
    }

    /// <inheritdoc/>
    public string BuildResponseJson(GatewayChatResponse response)
    {
        var message = new JsonObject
        {
            ["role"] = "assistant",
            ["content"] = response.Text,
        };

        if (response.ToolCalls.Count > 0)
        {
            var toolCalls = new JsonArray();
            foreach (var toolCall in response.ToolCalls)
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

            message["tool_calls"] = toolCalls;
        }

        var root = new JsonObject
        {
            ["id"] = response.Id,
            ["object"] = "chat.completion",
            ["created"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["model"] = response.Model,
            ["choices"] = new JsonArray(new JsonObject
            {
                ["index"] = 0,
                ["message"] = message,
                ["finish_reason"] = MapFinishReason(response.FinishReason),
                ["logprobs"] = null,
            }),
            ["usage"] = BuildUsage(response.Usage),
        };

        return root.ToJsonString();
    }

    /// <inheritdoc/>
    public string BuildErrorJson(int statusCode, string message, string? code)
    {
        return new JsonObject
        {
            ["error"] = new JsonObject
            {
                ["message"] = message,
                ["type"] = MapErrorType(statusCode, code),
                ["param"] = null,
                ["code"] = code,
            },
        }.ToJsonString();
    }

    /// <inheritdoc/>
    public IInboundStreamRenderer CreateRenderer(string responseId, string model, long createdAt)
    {
        return new ChatRenderer(responseId, model, createdAt);
    }

    /// <summary>
    /// 结束原因到 OpenAI finish_reason 的映射，供流式渲染复用.
    /// </summary>
    internal static string MapFinishReason(GatewayFinishReason reason)
    {
        return reason switch
        {
            GatewayFinishReason.Length => "length",
            GatewayFinishReason.ToolCalls => "tool_calls",
            GatewayFinishReason.ContentFilter => "content_filter",
            _ => "stop",
        };
    }

    internal static JsonObject BuildUsage(GatewayUsage? usage)
    {
        var promptTokens = usage?.PromptTokens ?? 0;
        var completionTokens = usage?.CompletionTokens ?? 0;
        return new JsonObject
        {
            ["prompt_tokens"] = promptTokens,
            ["completion_tokens"] = completionTokens,
            ["total_tokens"] = promptTokens + completionTokens,
        };
    }

    private static void ParseStop(JsonElement body, GatewayChatRequest request)
    {
        if (body.ValueKind != JsonValueKind.Object || !body.TryGetProperty("stop", out var stop))
        {
            return;
        }

        var stops = new List<string>();
        if (stop.ValueKind == JsonValueKind.String)
        {
            stops.Add(stop.GetString()!);
        }
        else if (stop.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in stop.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    stops.Add(item.GetString()!);
                }
            }
        }

        if (stops.Count > 0)
        {
            request.StopSequences = stops;
        }
    }

    private static void ParseMessages(JsonElement body, GatewayChatRequest request)
    {
        var messages = GatewayJsonReader.Array(body, "messages");
        if (messages == null)
        {
            throw new GatewayProtocolException(400, "'messages' is required.", "invalid_request_error");
        }

        foreach (var item in messages.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var role = GatewayJsonReader.String(item, "role") switch
            {
                "system" or "developer" => GatewayRole.System,
                "assistant" => GatewayRole.Assistant,
                "tool" => GatewayRole.Tool,
                _ => GatewayRole.User,
            };

            var message = new GatewayMessage(role);
            if (item.TryGetProperty("content", out var content))
            {
                if (content.ValueKind == JsonValueKind.String)
                {
                    message.Text = content.GetString();
                }
                else if (content.ValueKind == JsonValueKind.Array)
                {
                    message.Parts = new List<GatewayContentPart>();
                    var text = new System.Text.StringBuilder();
                    foreach (var part in content.EnumerateArray())
                    {
                        var type = GatewayJsonReader.String(part, "type");
                        if (type == "text" && GatewayJsonReader.String(part, "text") is { } partText)
                        {
                            message.Parts.Add(new GatewayTextPart(partText));
                            text.Append(partText);
                        }
                        else if (type == "image_url"
                            && GatewayJsonReader.Object(part, "image_url") is { } imageUrl
                            && GatewayJsonReader.String(imageUrl, "url") is { } url)
                        {
                            message.Parts.Add(new GatewayImagePart(url));
                        }
                    }

                    message.Text = text.Length > 0 ? text.ToString() : message.Text;
                }
            }

            var toolCalls = GatewayJsonReader.Array(item, "tool_calls");
            if (toolCalls != null)
            {
                message.ToolCalls = new List<GatewayToolCall>();
                foreach (var toolCall in toolCalls.Value.EnumerateArray())
                {
                    if (GatewayJsonReader.Object(toolCall, "function") is not { } function)
                    {
                        continue;
                    }

                    message.ToolCalls.Add(new GatewayToolCall(
                        GatewayJsonReader.String(toolCall, "id") ?? GatewayIds.New("call_"),
                        GatewayJsonReader.String(function, "name") ?? string.Empty,
                        GatewayJsonReader.String(function, "arguments") ?? "{}"));
                }
            }

            message.ToolCallId = GatewayJsonReader.String(item, "tool_call_id");
            message.Name = GatewayJsonReader.String(item, "name");
            request.Messages.Add(message);
        }
    }

    private static void ParseTools(JsonElement body, GatewayChatRequest request)
    {
        var tools = GatewayJsonReader.Array(body, "tools");
        if (tools != null)
        {
            request.Tools = new List<GatewayToolDefinition>();
            foreach (var tool in tools.Value.EnumerateArray())
            {
                if (GatewayJsonReader.Object(tool, "function") is not { } function)
                {
                    continue;
                }

                var name = GatewayJsonReader.String(function, "name");
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                request.Tools.Add(new GatewayToolDefinition(name)
                {
                    Description = GatewayJsonReader.String(function, "description"),
                    ParametersSchema = GatewayJsonReader.Object(function, "parameters"),
                });
            }

            if (request.Tools.Count == 0)
            {
                request.Tools = null;
            }
        }

        if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("tool_choice", out var toolChoice))
        {
            request.ToolChoice = toolChoice.ValueKind switch
            {
                JsonValueKind.String => toolChoice.GetString() switch
                {
                    "none" => new GatewayToolChoice(GatewayToolChoiceMode.None),
                    "required" => new GatewayToolChoice(GatewayToolChoiceMode.Required),
                    _ => new GatewayToolChoice(GatewayToolChoiceMode.Auto),
                },
                JsonValueKind.Object when GatewayJsonReader.Object(toolChoice, "function") is { } function
                    => new GatewayToolChoice(GatewayToolChoiceMode.Function, GatewayJsonReader.String(function, "name")),
                _ => null,
            };
        }
    }

    private static string MapErrorType(int statusCode, string? code)
    {
        return statusCode switch
        {
            401 => "invalid_request_error",
            403 => "invalid_request_error",
            404 => "invalid_request_error",
            429 => code == "quota_exceeded" ? "insufficient_quota" : "rate_limit_error",
            >= 500 => "api_error",
            _ => "invalid_request_error",
        };
    }

    private sealed class ChatRenderer : IInboundStreamRenderer
    {
        private readonly string _id;
        private readonly string _model;
        private readonly long _createdAt;
        private bool _roleSent;

        public ChatRenderer(string id, string model, long createdAt)
        {
            _id = id;
            _model = model;
            _createdAt = createdAt;
        }

        public IEnumerable<string> Render(GatewayStreamEvent evt)
        {
            switch (evt)
            {
                case GatewayStreamEvent.Started:
                    yield return Chunk(new JsonObject { ["role"] = "assistant", ["content"] = string.Empty }, null);
                    _roleSent = true;
                    break;

                case GatewayStreamEvent.TextDelta textDelta:
                    if (!_roleSent)
                    {
                        yield return Chunk(new JsonObject { ["role"] = "assistant", ["content"] = string.Empty }, null);
                        _roleSent = true;
                    }

                    yield return Chunk(new JsonObject { ["content"] = textDelta.Text }, null);
                    break;

                case GatewayStreamEvent.ToolCallStarted toolCallStarted:
                    if (!_roleSent)
                    {
                        yield return Chunk(new JsonObject { ["role"] = "assistant", ["content"] = string.Empty }, null);
                        _roleSent = true;
                    }

                    yield return Chunk(new JsonObject
                    {
                        ["tool_calls"] = new JsonArray(new JsonObject
                        {
                            ["index"] = toolCallStarted.Index,
                            ["id"] = toolCallStarted.Id,
                            ["type"] = "function",
                            ["function"] = new JsonObject
                            {
                                ["name"] = toolCallStarted.Name,
                                ["arguments"] = string.Empty,
                            },
                        }),
                    }, null);
                    break;

                case GatewayStreamEvent.ToolCallArgumentsDelta argumentsDelta:
                    yield return Chunk(new JsonObject
                    {
                        ["tool_calls"] = new JsonArray(new JsonObject
                        {
                            ["index"] = argumentsDelta.Index,
                            ["function"] = new JsonObject { ["arguments"] = argumentsDelta.ArgumentsDelta },
                        }),
                    }, null);
                    break;

                case GatewayStreamEvent.Finished finished:
                    yield return Chunk(new JsonObject(), MapFinishReason(finished.Reason));
                    if (finished.Usage != null)
                    {
                        yield return new JsonObject
                        {
                            ["id"] = _id,
                            ["object"] = "chat.completion.chunk",
                            ["created"] = _createdAt,
                            ["model"] = _model,
                            ["choices"] = new JsonArray(),
                            ["usage"] = BuildUsage(finished.Usage),
                        }.ToJsonString();
                    }

                    break;
            }
        }

        public IEnumerable<string> RenderDone()
        {
            yield return "[DONE]";
        }

        private string Chunk(JsonObject delta, string? finishReason)
        {
            return new JsonObject
            {
                ["id"] = _id,
                ["object"] = "chat.completion.chunk",
                ["created"] = _createdAt,
                ["model"] = _model,
                ["choices"] = new JsonArray(new JsonObject
                {
                    ["index"] = 0,
                    ["delta"] = delta,
                    ["finish_reason"] = finishReason,
                }),
            }.ToJsonString();
        }
    }
}
