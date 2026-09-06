using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MoAI.Gateway.Protocols.Inbound;

/// <summary>
/// OpenAI Responses 入口协议（/v1/responses）.
/// </summary>
public class OpenAIResponsesInbound : IInboundProtocol
{
    /// <inheritdoc/>
    public GatewayInboundFormat Format => GatewayInboundFormat.OpenAIResponses;

    /// <inheritdoc/>
    public string IdPrefix => "resp_";

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
            MaxOutputTokens = GatewayJsonReader.Int(body, "max_output_tokens"),
            User = GatewayJsonReader.String(body, "user"),
        };

        var instructions = GatewayJsonReader.String(body, "instructions");
        if (!string.IsNullOrEmpty(instructions))
        {
            request.Messages.Add(new GatewayMessage(GatewayRole.System, instructions));
        }

        if (body.TryGetProperty("input", out var input))
        {
            if (input.ValueKind == JsonValueKind.String)
            {
                request.Messages.Add(new GatewayMessage(GatewayRole.User, input.GetString()));
            }
            else if (input.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in input.EnumerateArray())
                {
                    ParseInputItem(item, request);
                }
            }
        }

        ParseTools(body, request);
        return request;
    }

    /// <inheritdoc/>
    public string BuildResponseJson(GatewayChatResponse response)
    {
        var output = BuildOutputItems(response.Text, response.ToolCalls);

        var root = new JsonObject
        {
            ["id"] = response.Id,
            ["object"] = "response",
            ["created_at"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["status"] = "completed",
            ["model"] = response.Model,
            ["output"] = output,
            ["error"] = null,
            ["incomplete_details"] = response.FinishReason == GatewayFinishReason.Length
                ? new JsonObject { ["reason"] = "max_output_tokens" }
                : null,
            ["usage"] = BuildUsage(response.Usage),
        };

        return root.ToJsonString();
    }

    /// <inheritdoc/>
    public string BuildErrorJson(int statusCode, string message, string? code)
    {
        var type = statusCode switch
        {
            401 => "invalid_request_error",
            403 => "invalid_request_error",
            404 => "invalid_request_error",
            429 => code == "quota_exceeded" ? "insufficient_quota" : "rate_limit_error",
            >= 500 => "api_error",
            _ => "invalid_request_error",
        };

        return new JsonObject
        {
            ["error"] = new JsonObject
            {
                ["message"] = message,
                ["type"] = type,
                ["param"] = null,
                ["code"] = code,
            },
        }.ToJsonString();
    }

    /// <inheritdoc/>
    public IInboundStreamRenderer CreateRenderer(string responseId, string model, long createdAt)
    {
        return new ResponsesRenderer(responseId, model, createdAt);
    }

    internal static JsonObject BuildUsage(GatewayUsage? usage)
    {
        var promptTokens = usage?.PromptTokens ?? 0;
        var completionTokens = usage?.CompletionTokens ?? 0;
        return new JsonObject
        {
            ["input_tokens"] = promptTokens,
            ["input_tokens_details"] = new JsonObject { ["cached_tokens"] = 0 },
            ["output_tokens"] = completionTokens,
            ["output_tokens_details"] = new JsonObject { ["reasoning_tokens"] = 0 },
            ["total_tokens"] = promptTokens + completionTokens,
        };
    }

    private static JsonArray BuildOutputItems(string? text, IReadOnlyList<GatewayToolCall> toolCalls)
    {
        var output = new JsonArray();
        if (!string.IsNullOrEmpty(text))
        {
            output.Add(new JsonObject
            {
                ["type"] = "message",
                ["id"] = GatewayIds.New("msg_"),
                ["status"] = "completed",
                ["role"] = "assistant",
                ["content"] = new JsonArray(new JsonObject
                {
                    ["type"] = "output_text",
                    ["text"] = text,
                    ["annotations"] = new JsonArray(),
                }),
            });
        }

        foreach (var toolCall in toolCalls)
        {
            output.Add(new JsonObject
            {
                ["type"] = "function_call",
                ["id"] = GatewayIds.New("fc_"),
                ["call_id"] = toolCall.Id,
                ["name"] = toolCall.Name,
                ["arguments"] = toolCall.ArgumentsJson,
                ["status"] = "completed",
            });
        }

        return output;
    }

    private static void ParseInputItem(JsonElement item, GatewayChatRequest request)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var type = GatewayJsonReader.String(item, "type");
        if (type == "function_call")
        {
            request.Messages.Add(new GatewayMessage(GatewayRole.Assistant)
            {
                ToolCalls = new List<GatewayToolCall>
                {
                    new(
                        GatewayJsonReader.String(item, "call_id") ?? GatewayIds.New("call_"),
                        GatewayJsonReader.String(item, "name") ?? string.Empty,
                        GatewayJsonReader.String(item, "arguments") ?? "{}"),
                },
            });
            return;
        }

        if (type == "function_call_output")
        {
            request.Messages.Add(new GatewayMessage(GatewayRole.Tool)
            {
                ToolCallId = GatewayJsonReader.String(item, "call_id"),
                Text = GatewayJsonReader.String(item, "output") ?? string.Empty,
            });
            return;
        }

        if (type != null && type != "message")
        {
            // reasoning / item_reference 等类型忽略.
            return;
        }

        var role = GatewayJsonReader.String(item, "role") switch
        {
            "assistant" => GatewayRole.Assistant,
            "system" or "developer" => GatewayRole.System,
            _ => GatewayRole.User,
        };

        if (!item.TryGetProperty("content", out var content))
        {
            return;
        }

        if (content.ValueKind == JsonValueKind.String)
        {
            request.Messages.Add(new GatewayMessage(role, content.GetString()));
            return;
        }

        if (content.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var message = new GatewayMessage(role);
        foreach (var part in content.EnumerateArray())
        {
            var partType = GatewayJsonReader.String(part, "type");
            if (partType is "input_text" or "output_text" or "text")
            {
                AppendText(message, GatewayJsonReader.String(part, "text") ?? string.Empty);
            }
            else if (partType == "input_image")
            {
                var url = GatewayJsonReader.String(part, "image_url");
                if (!string.IsNullOrEmpty(url))
                {
                    message.Parts ??= new List<GatewayContentPart>();
                    message.Parts.Add(new GatewayImagePart(url));
                }
            }
        }

        request.Messages.Add(message);
    }

    private static void AppendText(GatewayMessage message, string text)
    {
        message.Text = message.Text == null ? text : message.Text + text;
        message.Parts ??= new List<GatewayContentPart>();
        message.Parts.Add(new GatewayTextPart(text));
    }

    private static void ParseTools(JsonElement body, GatewayChatRequest request)
    {
        var tools = GatewayJsonReader.Array(body, "tools");
        if (tools != null)
        {
            request.Tools = new List<GatewayToolDefinition>();
            foreach (var tool in tools.Value.EnumerateArray())
            {
                if (GatewayJsonReader.String(tool, "type") != "function")
                {
                    continue;
                }

                var name = GatewayJsonReader.String(tool, "name");
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                request.Tools.Add(new GatewayToolDefinition(name)
                {
                    Description = GatewayJsonReader.String(tool, "description"),
                    ParametersSchema = GatewayJsonReader.Object(tool, "parameters"),
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
                JsonValueKind.Object when GatewayJsonReader.String(toolChoice, "type") == "function"
                    => new GatewayToolChoice(GatewayToolChoiceMode.Function, GatewayJsonReader.String(toolChoice, "name")),
                _ => null,
            };
        }
    }

    private sealed class ResponsesRenderer : IInboundStreamRenderer
    {
        private readonly string _id;
        private readonly string _model;
        private readonly long _createdAt;
        private readonly StringBuilder _text = new();
        private readonly List<(string ItemId, string CallId, string Name, StringBuilder Args)> _toolCalls = new();
        private readonly Dictionary<int, int> _outputIndexByToolIndex = new();
        private readonly Dictionary<int, string> _itemIdByToolIndex = new();
        private string? _messageItemId;
        private int _messageOutputIndex = -1;
        private int _nextOutputIndex;
        private bool _createdSent;

        public ResponsesRenderer(string id, string model, long createdAt)
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
                    yield return Event("response.created", new JsonObject { ["response"] = ResponseSkeleton("in_progress") });
                    _createdSent = true;
                    break;

                case GatewayStreamEvent.TextDelta textDelta:
                    foreach (var added in EnsureMessageItem())
                    {
                        yield return added;
                    }

                    _text.Append(textDelta.Text);
                    yield return Event("response.output_text.delta", new JsonObject
                    {
                        ["item_id"] = _messageItemId,
                        ["output_index"] = _messageOutputIndex,
                        ["content_index"] = 0,
                        ["delta"] = textDelta.Text,
                    });
                    break;

                case GatewayStreamEvent.ToolCallStarted toolCallStarted:
                {
                    var outputIndex = _nextOutputIndex++;
                    var itemId = GatewayIds.New("fc_");
                    _outputIndexByToolIndex[toolCallStarted.Index] = outputIndex;
                    _itemIdByToolIndex[toolCallStarted.Index] = itemId;
                    _toolCalls.Add((itemId, toolCallStarted.Id, toolCallStarted.Name, new StringBuilder()));
                    yield return Event("response.output_item.added", new JsonObject
                    {
                        ["output_index"] = outputIndex,
                        ["item"] = new JsonObject
                        {
                            ["type"] = "function_call",
                            ["id"] = itemId,
                            ["call_id"] = toolCallStarted.Id,
                            ["name"] = toolCallStarted.Name,
                            ["arguments"] = string.Empty,
                            ["status"] = "in_progress",
                        },
                    });
                    break;
                }

                case GatewayStreamEvent.ToolCallArgumentsDelta argumentsDelta:
                {
                    var outputIndex = _outputIndexByToolIndex.TryGetValue(argumentsDelta.Index, out var idx) ? idx : argumentsDelta.Index;
                    var itemId = _itemIdByToolIndex.TryGetValue(argumentsDelta.Index, out var id) ? id : string.Empty;
                    var tracked = _toolCalls.First(x => x.ItemId == itemId);
                    tracked.Args.Append(argumentsDelta.ArgumentsDelta);
                    yield return Event("response.function_call_arguments.delta", new JsonObject
                    {
                        ["item_id"] = itemId,
                        ["output_index"] = outputIndex,
                        ["delta"] = argumentsDelta.ArgumentsDelta,
                    });
                    break;
                }

                case GatewayStreamEvent.Finished finished:
                {
                    foreach (var item in CloseItems())
                    {
                        yield return item;
                    }

                    var response = ResponseSkeleton("completed");
                    response["output"] = BuildOutputItems(_text.ToString(), _toolCalls.Select(x => new GatewayToolCall(x.CallId, x.Name, x.Args.ToString())).ToList());
                    response["usage"] = BuildUsage(finished.Usage);
                    response["incomplete_details"] = finished.Reason == GatewayFinishReason.Length
                        ? new JsonObject { ["reason"] = "max_output_tokens" }
                        : null;
                    yield return Event("response.completed", new JsonObject { ["response"] = response });
                    break;
                }
            }
        }

        public IEnumerable<string> RenderDone()
        {
            return Enumerable.Empty<string>();
        }

        private IEnumerable<string> CloseItems()
        {
            if (_messageItemId != null)
            {
                var text = _text.ToString();
                yield return Event("response.output_text.done", new JsonObject
                {
                    ["item_id"] = _messageItemId,
                    ["output_index"] = _messageOutputIndex,
                    ["content_index"] = 0,
                    ["text"] = text,
                });
                yield return Event("response.content_part.done", new JsonObject
                {
                    ["item_id"] = _messageItemId,
                    ["output_index"] = _messageOutputIndex,
                    ["content_index"] = 0,
                    ["part"] = new JsonObject { ["type"] = "output_text", ["text"] = text, ["annotations"] = new JsonArray() },
                });
                yield return Event("response.output_item.done", new JsonObject
                {
                    ["output_index"] = _messageOutputIndex,
                    ["item"] = new JsonObject
                    {
                        ["type"] = "message",
                        ["id"] = _messageItemId,
                        ["status"] = "completed",
                        ["role"] = "assistant",
                        ["content"] = new JsonArray(new JsonObject
                        {
                            ["type"] = "output_text",
                            ["text"] = text,
                            ["annotations"] = new JsonArray(),
                        }),
                    },
                });
            }

            for (var i = 0; i < _toolCalls.Count; i++)
            {
                var toolCall = _toolCalls[i];
                var args = toolCall.Args.ToString();
                var outputIndex = _outputIndexByToolIndex.TryGetValue(i, out var idx) ? idx : i;
                yield return Event("response.output_item.done", new JsonObject
                {
                    ["output_index"] = outputIndex,
                    ["item"] = new JsonObject
                    {
                        ["type"] = "function_call",
                        ["id"] = toolCall.ItemId,
                        ["call_id"] = toolCall.CallId,
                        ["name"] = toolCall.Name,
                        ["arguments"] = args,
                        ["status"] = "completed",
                    },
                });
            }
        }

        private IEnumerable<string> EnsureMessageItem()
        {
            if (_messageItemId != null)
            {
                yield break;
            }

            _messageItemId = GatewayIds.New("msg_");
            _messageOutputIndex = _nextOutputIndex++;

            // 在首个文本增量时补发 message item 事件.
            yield return Event("response.output_item.added", new JsonObject
            {
                ["output_index"] = _messageOutputIndex,
                ["item"] = new JsonObject
                {
                    ["type"] = "message",
                    ["id"] = _messageItemId,
                    ["status"] = "in_progress",
                    ["role"] = "assistant",
                    ["content"] = new JsonArray(),
                },
            });
            yield return Event("response.content_part.added", new JsonObject
            {
                ["item_id"] = _messageItemId,
                ["output_index"] = _messageOutputIndex,
                ["content_index"] = 0,
                ["part"] = new JsonObject { ["type"] = "output_text", ["text"] = string.Empty, ["annotations"] = new JsonArray() },
            });
        }

        private JsonObject ResponseSkeleton(string status)
        {
            return new JsonObject
            {
                ["id"] = _id,
                ["object"] = "response",
                ["created_at"] = _createdAt,
                ["status"] = status,
                ["model"] = _model,
                ["output"] = new JsonArray(),
                ["error"] = null,
                ["incomplete_details"] = null,
                ["usage"] = null,
            };
        }

        private static string Event(string type, JsonObject payload)
        {
            payload["type"] = type;
            return payload.ToJsonString();
        }
    }
}
