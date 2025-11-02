using MoAI.Infra.Services;
using Yitter.IdGenerator;

namespace MoAI.Infra.Defaults;

/// <summary>
/// Id 提供器.
/// </summary>
public class DefaultIdProvider : IIdProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultIdProvider"/> class.
    /// </summary>
    /// <param name="workId"></param>
    public DefaultIdProvider(ushort workId)
    {
        IdGeneratorOptions? options = new(workId) { SeqBitLength = 10 };
        YitIdHelper.SetIdGenerator(options);
    }

    /// <inheritdoc />
    public long NextId()
    {
        return YitIdHelper.NextId();
    }

    /// <inheritdoc />
    public long GeneratorId(out string value)
    {
        long id = YitIdHelper.NextId();
        value = id.ToString("x16");
        return id;
    }

    /// <inheritdoc />
    public string GeneratorKey()
    {
        long id = YitIdHelper.NextId();
        return id.ToString("x16");
    }
}