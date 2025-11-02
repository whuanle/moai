using NJsonSchema;
using NJsonSchema.Generation.TypeMappers;

namespace MoAI.Swaggers;

/// <summary>
/// DateTimeOffset => 字符串时间戳.
/// </summary>
public class DateTimeOffsetTypeMapper : ITypeMapper
{
    private static readonly string _example = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

    /// <inheritdoc/>
    Type ITypeMapper.MappedType => typeof(DateTimeOffset);

    /// <inheritdoc/>
    bool ITypeMapper.UseReference => false;

    /// <inheritdoc/>
    void ITypeMapper.GenerateSchema(JsonSchema schema, TypeMapperContext context)
    {
        schema.Type = JsonObjectType.String;
        schema.Format = JsonFormatStrings.Long;
        schema.Example = _example;
        schema.Minimum = 0;
    }
}