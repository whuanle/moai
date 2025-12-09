namespace MoAI.AI.Models;

/// <summary>
/// ModelAbilities.
/// </summary>
public class ModelAbilities
{
    /// <summary>
    /// Whether model supports file upload
    /// </summary>
    public bool? Files { get; init; } = default!;

    /// <summary>
    /// Whether model supports function call
    /// </summary>
    public bool? FunctionCall { get; init; } = default!;

    /// <summary>
    /// Whether model supports image output
    /// </summary>
    public bool? ImageOutput { get; init; } = default!;

    /// <summary>
    /// Whether model supports vision
    /// </summary>
    public bool? Vision { get; init; } = default!;
}
