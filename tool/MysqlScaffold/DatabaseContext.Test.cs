using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace MoAI.Database;

/// <summary>
/// DatabaseContext.
/// </summary>
public partial class DatabaseContext
{
    /// <summary>
    /// 审计属性.
    /// </summary>
    /// <param name="args"></param>
    protected static void AuditFilter(EntityEntryEventArgs args)
    {
    }
}