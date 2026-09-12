using System.Text.Json;
using MoAI.AI.Services;
using OpenSandbox.Models;
using Xunit;

namespace MoAI.AI.Core.Tests;

/// <summary>
/// 沙箱执行结果到 JSON 的映射.
/// </summary>
public class SandboxExecutionMappingTests
{
    [Fact]
    public void MapExecution_CollectsStdoutStderrAndExitCode()
    {
        var execution = new Execution { ExitCode = 0 };
        execution.Logs.Stdout.Add(new OutputMessage { Text = "hello ", Timestamp = 1 });
        execution.Logs.Stdout.Add(new OutputMessage { Text = "world", Timestamp = 2 });
        execution.Logs.Stderr.Add(new OutputMessage { Text = "warn", Timestamp = 3 });

        var json = OpenSandboxService.MapExecution(execution);

        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("hello world", doc.RootElement.GetProperty("stdout").GetString());
        Assert.Equal("warn", doc.RootElement.GetProperty("stderr").GetString());
        Assert.Equal(0, doc.RootElement.GetProperty("exitCode").GetInt32());
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("error").ValueKind);
    }

    [Fact]
    public void MapExecution_WithError_MarksUnsuccessful()
    {
        var execution = new Execution
        {
            Error = new ExecutionError { Name = "ValueError", Value = "bad", Timestamp = 1, Traceback = ["line1"] },
        };

        var json = OpenSandboxService.MapExecution(execution);

        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("ValueError", doc.RootElement.GetProperty("error").GetProperty("name").GetString());
    }
}
