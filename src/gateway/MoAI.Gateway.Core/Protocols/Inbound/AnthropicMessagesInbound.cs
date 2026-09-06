using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MoAI.Gateway.Protocols.Inbound;

/// <summary>
/// Anthropic Messages 入口协议（/v1/messages）.
/// </summary>
public class AnthropicMessagesInbound : IInboundProtocol
{
    private const int DefaultMaxTokens = 4096;

    /// <inheritdoc/>
    public GatewayInboundFormat Format => GatewayInboundFormat.AnthropicMessages;

    /// <inheritdoc/>
    public string IdPrefix => "msg_";

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
            MaxOutputTokens = GatewayJsonReader.Int(body, "max_tokens") ?? DefaultMaxTokens,
        };

        var stopSequences = GatewayJsonReader.Array(body, "stop_sequences");
        if (stopSequences != null)
        {
            var stops = new List<string>();
            foreach (var item in stopSequences.Value.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    stops.Add(item.GetString()!);
                }
            }

            if (stops.Count > 0)
            {
                request.StopSequences = stops;
            }
        }

        // system 提示.
        if (body.TryGetProperty("system", out var system))
        {
            var systemText = ReadBlocksText(system);
            if (!string.IsNullOrEmpty(systemText))
            {
                request.Messages.Add(new GatewayMessage(GatewayRole.System, systemText));
            }
        }

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

            var role = GatewayJsonReader.String(item, "role") == "assistant" ? GatewayRole.Assistant : GatewayRole.User;
            if (!item.TryGetProperty("content", out var content))
            {
                continue;
            }

            if (content.ValueKind == JsonValueKind.String)
            {
                request.Messages.Add(new GatewayMessage(role, content.GetString()));
                continue;
            }

            if (content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            // content 块数组：tool_result 拆为独立 Tool 消息，text/image 聚合为同角色消息.
            GatewayMessage? current = null;
            foreach (var block in content.EnumerateArray())
            {
                var type = GatewayJsonReader.String(block, "type");
                if (type == "text")
                {
                    current ??= new GatewayMessage(role);
                    AppendText(current, GatewayJsonReader.String(block, "text") ?? string.Empty);
                }
                else if (type == "image")
                {
                    var image = ParseImage(block);
                    if (image != null)
                    {
                        current ??= new GatewayMessage(role);
                        current.Parts ??= new List<GatewayContentPart>();
                        current.Parts.Add(image);
                    }
                }
                else if (type == "tool_use")
                {
                    current = null;
                    var toolMessage = new GatewayMessage(role)
                    {
                        ToolCalls = new List<GatewayToolCall>
                        {
                            new(
                                GatewayJsonReader.String(block, "id") ?? GatewayIds.New("call_"),
                                GatewayJsonReader.String(block, "name") ?? string.Empty,
                                SerializeJson(block, "input")),
                        },
                    };
                    request.Messages.Add(toolMessage);
                }
                else if (type == "tool_result")
                {
                    current = null;
                    var result = new GatewayMessage(GatewayRole.Tool)
                    {
                        ToolCallId = GatewayJsonReader.String(block, "tool_use_id"),
                        Text = ReadBlocksText(block.TryGetProperty("content", out var inner) ? inner : default),
                    };
                    request.Messages.Add(result);
                }
            }

            if (current != null)
            {
                request.Messages.Add(current);
            }
        }

        ParseTools(body, request);
        return request;
    }

    /// <inheritdoc/>
    public string BuildResponseJson(GatewayChatResponse response)
    {
        var content = new JsonArray();
        if (!string.IsNullOrEmpty(response.Text))
        {
            content.Add(new JsonObject { ["type"] = "text", ["text"] = response.Text });
        }

        foreach (var toolCall in response.ToolCalls)
        {
            content.Add(new JsonObject
            {
                ["type"] = "tool_use",
                ["id"] = toolCall.Id,
                ["name"] = toolCall.Name,
                ["input"] = ParseJsonObject(toolCall.ArgumentsJson),
            });
        }

        var root = new JsonObject
        {
            ["id"] = response.Id,
            ["type"] = "message",
            ["role"] = "assistant",
            ["model"] = response.Model,
            ["content"] = content,
            ["stop_reason"] = MapStopReason(response.FinishReason),
            ["stop_sequence"] = null,
            ["usage"] = new JsonObject
            {
                ["input_tokens"] = response.Usage?.PromptTokens ?? 0,
                ["output_tokens"] = response.Usage?.CompletionTokens ?? 0,
            },
        };

        return root.ToJsonString();
    }

    /// <inheritdoc/>
    public string BuildErrorJson(int statusCode, string message, string? code)
    {
        var type = statusCode switch
        {
            400 => "invalid_request_error",
            401 => "authentication_error",
            403 => "permission_error",
            404 => "not_found_error",
            429 => "rate_limit_error",
            >= 500 => "api_error",
            _ => "invalid_request_error",
        };

        return new JsonObject
        {
            ["type"] = "error",
            ["error"] = new JsonObject
            {
                ["type"] = type,
                ["message"] = message,
            },
        }.ToJsonString();
    }

    /// <inheritdoc/>
    public IInboundStreamRenderer CreateRenderer(string responseId, string model, long createdAt)
    {
        return new MessagesRenderer(responseId, model, createdAt);
    }

    /// <summary>
    /// 结束原因到 Anthropic stop_reason 的映射.
    /// </summary>
    internal static string MapStopReason(GatewayFinishReason reason)
    {
        return reason switch
        {
            GatewayFinishReason.Length => "max_tokens",
            GatewayFinishReason.ToolCalls => "tool_use",
            GatewayFinishReason.ContentFilter => "refusal",
            GatewayFinishReason.Stop => "end_turn",
            _ => "end_turn",
        };
    }

    private static string? ReadBlocksText(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString();
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            var sb = new StringBuilder();
            foreach (var block in element.EnumerateArray())
            {
                if (GatewayJsonReader.String(block, "type") == "text")
                {
                    sb.Append(GatewayJsonReader.String(block, "text"));
                }
            }

            return sb.ToString();
        }

        return null;
    }

    private static GatewayImagePart? ParseImage(JsonElement block)
    {
        if (GatewayJsonReader.Object(block, "source") is not { } source)
        {
            return null;
        }

        var sourceType = GatewayJsonReader.String(source, "type");
        if (sourceType == "base64")
        {
            var mediaType = GatewayJsonReader.String(source, "media_type") ?? "image/png";
            var data = GatewayJsonReader.String(source, "data") ?? string.Empty;
            return new GatewayImagePart($"data:{mediaType};base64,{data}");
        }

        if (sourceType == "url")
        {
            var url = GatewayJsonReader.String(source, "url");
            return url == null ? null : new GatewayImagePart(url);
        }

        return null;
    }

    private static void AppendText(GatewayMessage message, string text)
    {
        message.Text = message.Text == null ? text : message.Text + text;
        if (message.Parts != null)
        {
            message.Parts.Add(new GatewayTextPart(text));
        }
    }

    private static string SerializeJson(JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.Object)
        {
            return value.GetRawText();
        }

        return "{}";
    }

    private static JsonNode ParseJsonObject(string json)
    {
        try
        {
            var node = JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            if (node is JsonObject obj)
            {
                return obj;
            }

            return new JsonObject { ["value"] = node };
        }
        catch (JsonException)
        {
            return new JsonObject();
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
                var name = GatewayJsonReader.String(tool, "name");
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                request.Tools.Add(new GatewayToolDefinition(name)
                {
                    Description = GatewayJsonReader.String(tool, "description"),
                    ParametersSchema = GatewayJsonReader.Object(tool, "input_schema"),
                });
            }

            if (request.Tools.Count == 0)
            {
                request.Tools = null;
            }
        }

        if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("tool_choice", out var toolChoice) && toolChoice.ValueKind == JsonValueKind.Object)
        {
            var type = GatewayJsonReader.String(toolChoice, "type");
            request.ToolChoice = type switch
            {
                "none" => new GatewayToolChoice(GatewayToolChoiceMode.None),
                "any" => new GatewayToolChoice(GatewayToolChoiceMode.Required),
                "tool" => new GatewayToolChoice(GatewayToolChoiceMode.Function, GatewayJsonReader.String(toolChoice, "name")),
                _ => new GatewayToolChoice(GatewayToolChoiceMode.Auto),
            };
        }
    }

    private sealed class MessagesRenderer : IInboundStreamRenderer
    {
        private readonly string _id;
        private readonly string _model;
        private readonly long _createdAt;
        private int _nextIndex;
        private int _textIndex;
        private bool _textOpen;
        private readonly List<(int Index, string Id, string Name, StringBuilder Args)> _toolCalls = new();
        private readonly Dictionary<int, int> _toolIndexByCallIndex = new();
        private readonly StringBuilder _text = new();

        public MessagesRenderer(string id, string model, long createdAt)
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
                    yield return Data(new JsonObject
                    {
                        ["type"] = "message_start",
                        ["message"] = MessageSkeleton("in_progress"),
                    });
                    break;

                case GatewayStreamEvent.TextDelta textDelta:
                    if (!_textOpen)
                    {
                        _textIndex = _nextIndex++;
                        _textOpen = true;
                        yield return Data(new JsonObject
                        {
                            ["type"] = "content_block_start",
                            ["index"] = _textIndex,
                            ["content_block"] = new JsonObject { ["type"] = "text", ["text"] = string.Empty },
                        });
                    }

                    _text.Append(textDelta.Text);
                    yield return Data(new JsonObject
                    {
                        ["type"] = "content_block_delta",
                        ["index"] = _textIndex,
                        ["delta"] = new JsonObject { ["type"] = "text_delta", ["text"] = textDelta.Text },
                    });
                    break;

                case GatewayStreamEvent.ToolCallStarted toolCallStarted:
                    if (_textOpen)
                    {
                        _textOpen = false;
                        yield return Data(BlockStop(_textIndex));
                    }

                    var blockIndex = _nextIndex++;
                    _toolIndexByCallIndex[toolCallStarted.Index] = blockIndex;
                    _toolCalls.Add((blockIndex, toolCallStarted.Id, toolCallStarted.Name, new StringBuilder()));
                    yield return Data(new JsonObject
                    {
                        ["type"] = "content_block_start",
                        ["index"] = blockIndex,
                        ["content_block"] = new JsonObject
                        {
                            ["type"] = "tool_use",
                            ["id"] = toolCallStarted.Id,
                            ["name"] = toolCallStarted.Name,
                            ["input"] = new JsonObject(),
                        },
                    });
                    break;

                case GatewayStreamEvent.ToolCallArgumentsDelta argumentsDelta:
                    var toolIndex = _toolIndexByCallIndex.TryGetValue(argumentsDelta.Index, out var idx) ? idx : argumentsDelta.Index;
                    var tracked = _toolCalls.FirstOrDefault(x => x.Index == toolIndex);
                    tracked.Args.Append(argumentsDelta.ArgumentsDelta);
                    yield return Data(new JsonObject
                    {
                        ["type"] = "content_block_delta",
                        ["index"] = toolIndex,
                        ["delta"] = new JsonObject { ["type"] = "input_json_delta", ["partial_json"] = argumentsDelta.ArgumentsDelta },
                    });
                    break;

                case GatewayStreamEvent.Finished finished:
                    if (_textOpen)
                    {
                        _textOpen = false;
                        yield return Data(BlockStop(_textIndex));
                    }

                    foreach (var toolCall in _toolCalls)
                    {
                        yield return Data(BlockStop(toolCall.Index));
                    }

                    yield return Data(new JsonObject
                    {
                        ["type"] = "message_delta",
                        ["delta"] = new JsonObject
                        {
                            ["stop_reason"] = MapStopReason(finished.Reason),
                            ["stop_sequence"] = null,
                        },
                        ["usage"] = new JsonObject
                        {
                            ["output_tokens"] = finished.Usage?.CompletionTokens ?? 0,
                        },
                    });
                    yield return Data(new JsonObject { ["type"] = "message_stop" });
                    break;
            }
        }

        public IEnumerable<string> RenderDone()
        {
            return Enumerable.Empty<string>();
        }

        private JsonObject MessageSkeleton(string status)
        {
            return new JsonObject
            {
                ["id"] = _id,
                ["type"] = "message",
                ["role"] = "assistant",
                ["model"] = _model,
                ["content"] = new JsonArray(),
                ["stop_reason"] = null,
                ["stop_sequence"] = null,
                ["usage"] = new JsonObject { ["input_tokens"] = 0, ["output_tokens"] = 0 },
            };
        }

        private static JsonObject BlockStop(int index)
        {
            return new JsonObject
            {
                ["type"] = "content_block_stop",
                ["index"] = index,
            };
        }

        private static string Data(JsonObject obj)
        {
            return obj.ToJsonString();
        }
    }
}
