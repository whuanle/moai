namespace MoAI.Infra.Models;

public class KeyValue<TKey, TValue>
{
    public TKey Key { get; set; } = default!;
    public TValue Value { get; set; } = default!;

    public KeyValue(TKey key, TValue value)
    {
        Value = value;
    }

    public KeyValue()
    {
    }
}
