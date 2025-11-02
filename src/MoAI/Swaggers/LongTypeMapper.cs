using NJsonSchema;
using NJsonSchema.Generation.TypeMappers;

namespace MoAI.Swaggers;

/// <summary>
/// Swagger 模型类过滤器.
/// </summary>
public class LongTypeMapper : ITypeMapper
{
    /// <inheritdoc/>
    Type ITypeMapper.MappedType => typeof(long);

    /// <inheritdoc/>
    bool ITypeMapper.UseReference => false;

    /// <summary>
    /// 生成模型类.
    /// </summary>
    /// <param name="schema"></param>
    /// <param name="context"></param>
    public void GenerateSchema(JsonSchema schema, TypeMapperContext context)
    {
        schema.Type = JsonObjectType.String;
        schema.Format = JsonFormatStrings.Long;
        schema.Example = "1415926535897934852";
        schema.Minimum = 0;
    }
}