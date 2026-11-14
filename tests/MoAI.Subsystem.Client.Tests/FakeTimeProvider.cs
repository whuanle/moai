namespace MoAI.Subsystem.Client.Tests;

/// <summary>可控时钟，测试 token 生命周期用。</summary>
internal sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = start;

    public override DateTimeOffset GetUtcNow() => Now;
}
