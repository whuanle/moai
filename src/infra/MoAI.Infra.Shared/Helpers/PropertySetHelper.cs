using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace MoAI.Infra.Helpers;

/// <summary>
/// 支持设置 init 属性设置属性设置器.
/// </summary>
public static class PropertySetHelper
{
    // 使用线程安全的字典缓存属性设置委托
    private static readonly ConcurrentDictionary<string, Action<object, object>> _setterCache = new();

    /// <summary>
    /// 设置对象的属性值，支持 init 属性。
    /// </summary>
    /// <typeparam name="TTarget">目标对象类型</typeparam>
    /// <typeparam name="TProp">属性类型</typeparam>
    /// <param name="target">目标对象</param>
    /// <param name="propertyExpression">指向属性的表达式</param>
    /// <param name="value">要设置的值</param>
    /// <returns>目标对象（链式调用支持）</returns>
    public static TTarget SetProperty<TTarget, TProp>(this TTarget target, Expression<Func<TTarget, TProp>> propertyExpression, TProp value)
        where TTarget : class
    {
        // 获取属性信息
        var memberExpression = propertyExpression.Body as MemberExpression;
        if (memberExpression == null)
        {
            throw new ArgumentException("The expression must be an attribute access expression.", nameof(propertyExpression));
        }

        var propertyInfo = memberExpression.Member as PropertyInfo;
        if (propertyInfo == null)
        {
            throw new ArgumentException("The expression must reference a property.", nameof(propertyExpression));
        }

        var targetType = target.GetType();
        var cacheKey = $"{targetType.Name}.{propertyInfo.Name}";

        var setter = _setterCache.GetOrAdd(cacheKey, _ => CreatePropertySetter(targetType, propertyInfo.Name));

        // 执行setter
        setter(target, value!);

        return target;
    }

    // 创建赋值委托.
    private static Action<object, object> CreatePropertySetter(Type targetType, string propertyName)
    {
        var propertyInfo = targetType.GetProperty(propertyName);
        if (propertyInfo == null)
        {
            ArgumentNullException.ThrowIfNull(propertyInfo, propertyName);
        }

        if (propertyInfo.SetMethod != null && propertyInfo.SetMethod.IsPublic)
        {
            // 创建表达式：(target, value) => ((TTarget)target).Property = (TProp)value
            var targetParam = Expression.Parameter(typeof(object), "target");
            var valueParam = Expression.Parameter(typeof(object), "value");

            var targetCast = Expression.Convert(targetParam, targetType);
            var valueCast = Expression.Convert(valueParam, propertyInfo.PropertyType);

            var propertyAccess = Expression.Property(targetCast, propertyInfo);
            var assign = Expression.Assign(propertyAccess, valueCast);

            var lambda = Expression.Lambda<Action<object, object>>(assign, targetParam, valueParam);
            return lambda.Compile();
        }

        throw new InvalidOperationException($"Property '{propertyName}' does not have a public setter.");
    }
}