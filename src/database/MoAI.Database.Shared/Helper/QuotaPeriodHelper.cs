namespace MoAI.Database.Helper;

/// <summary>
/// ai_model_limit 重置周期计算.
/// </summary>
public static class QuotaPeriodHelper
{
    /// <summary>
    /// 重置周期单位：0=不重置 1=小时 2=天 3=周 4=月.
    /// </summary>
    public static DateTimeOffset ComputePeriodEnd(DateTimeOffset start, int periodValue, int periodUnit)
    {
        return periodUnit switch
        {
            1 => start.AddHours(periodValue),
            2 => start.AddDays(periodValue),
            3 => start.AddDays(7.0 * periodValue),
            4 => start.AddMonths(periodValue),
            _ => DateTimeOffset.MaxValue,
        };
    }
}
