namespace MoAI.Infra.Models;

/// <summary>
/// SimpleGuid.
/// </summary>
public class SimpleGuid : Simple<Guid>
{
    /// <summary>
    /// int.
    /// </summary>
    /// <param name="value"></param>
    public static implicit operator Guid(SimpleGuid value) => value.Value;

    /// <summary>
    /// SimpleGuid.
    /// </summary>
    /// <param name="value"></param>
    public static implicit operator SimpleGuid(Guid value) => new SimpleGuid { Value = value };

    /// <summary>
    /// ToGuid.
    /// </summary>
    /// <returns></returns>
    public Guid ToGuid()
    {
        return this.Value;
    }

    /// <summary>
    /// ToSimpleGuid.
    /// </summary>
    /// <returns></returns>
    public SimpleGuid ToSimpleGuid()
    {
        return new SimpleGuid() { Value = Value };
    }
}