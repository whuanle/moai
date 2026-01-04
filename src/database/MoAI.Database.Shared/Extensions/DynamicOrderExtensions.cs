using MoAI.Infra.Models;
using System.Linq.Expressions;
using System.Reflection;

namespace MoAI.Database;

/// <summary>
/// 动态排序.
/// </summary>
public static class DynamicOrderExtensions
{
    /// <summary>
    /// 动态排序.
    /// </summary>
    /// <typeparam name="TSource">实体.</typeparam>
    /// <param name="source"></param>
    /// <param name="fields"></param>
    /// <returns></returns>
    public static IQueryable<TSource> DynamicOrder<TSource>(this IQueryable<TSource> source, IReadOnlyCollection<KeyValueBool> fields)
    {
        return ApplyDynamicOrder(source, fields);
    }

    /// <summary>
    /// 动态排序，如果没有设置，则使用 defaultKey 排序.
    /// </summary>
    /// <typeparam name="TSource">实体.</typeparam>
    /// <param name="source"></param>
    /// <param name="fields"></param>
    /// <param name="defaultKey"></param>
    /// <returns></returns>
    /// <typeparam name="TKey">键</typeparam>
    public static IQueryable<TSource> DynamicOrder<TSource, TKey>(this IQueryable<TSource> source, IReadOnlyCollection<KeyValueBool> fields, Expression<Func<TSource, TKey>> defaultKey)
    {
        if (fields == null || fields.Count == 0)
        {
            return source.OrderBy(defaultKey);
        }

        return ApplyDynamicOrder(source, fields);
    }

    /// <summary>
    /// 动态排序，如果没有设置，则使用 defaultKey 排序.
    /// </summary>
    /// <typeparam name="TSource">实体.</typeparam>
    /// <param name="source"></param>
    /// <param name="fields"></param>
    /// <param name="defaultKey"></param>
    /// <returns></returns>
    /// <typeparam name="TKey">键</typeparam>
    public static IQueryable<TSource> DynamicOrderByDescending<TSource, TKey>(this IQueryable<TSource> source, IReadOnlyCollection<KeyValueBool> fields, Expression<Func<TSource, TKey>> defaultKey)
    {
        if (fields == null || fields.Count == 0)
        {
            return source.OrderByDescending(defaultKey);
        }

        return ApplyDynamicOrder(source, fields);
    }

    private static IQueryable<TSource> ApplyDynamicOrder<TSource>(IQueryable<TSource> source, IReadOnlyCollection<KeyValueBool> fields)
    {
        if (fields == null || fields.Count == 0)
        {
            return source;
        }

        var parameter = Expression.Parameter(typeof(TSource), "x");
        var properties = typeof(TSource).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var field in fields)
        {
            // 忽略大小写查找属性
            var property = properties.FirstOrDefault(p => p.Name.Equals(field.Key, StringComparison.OrdinalIgnoreCase));
            if (property == null)
            {
                continue;
            }

            // 构建 x => x.Property 表达式
            var propertyAccess = Expression.MakeMemberAccess(parameter, property);
            var lambda = Expression.Lambda(propertyAccess, parameter);

            var methodName = field.Value ? "OrderByDescending" : "OrderBy";

            // 动态调用 Queryable.OrderBy / OrderByDescending
            var resultExpression = Expression.Call(
                typeof(Queryable),
                methodName,
                new Type[] { typeof(TSource), property.PropertyType },
                source.Expression,
                Expression.Quote(lambda));

            source = source.Provider.CreateQuery<TSource>(resultExpression);
        }

        return source;
    }
}