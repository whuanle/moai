namespace MoAI.Infra.Models;

/// <summary>
/// kv.
/// </summary>
/// <typeparam name="TKey">key.</typeparam>
/// <typeparam name="TValue">value.</typeparam>
public class KeyValue<TKey, TValue>
{
    /// <summary>
    /// Key.
    /// </summary>
    public TKey Key { get; set; } = default!;

    /// <summary>
    /// Value.
    /// </summary>
    public TValue Value { get; set; } = default!;

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyValue{TKey, TValue}"/> class.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    public KeyValue(TKey key, TValue value)
    {
        Key = key;
        Value = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="KeyValue{TKey, TValue}"/> class.
    /// </summary>
    public KeyValue()
    {
    }
}
