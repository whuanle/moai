using FluentValidation;

namespace MoAI.Infra.Defaults;

/// <summary>
/// 模型验证.
/// </summary>
/// <typeparam name="T">模型类型.</typeparam>
public interface IModelValidator<T>
    where T : class, new()
{
    /// <summary>
    /// 模型验证.
    /// </summary>
    /// <param name="validate"></param>
    void Validate(AbstractValidator<T> validate);
}