using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace MoAI.Database;

public partial class DatabaseContext
{
    protected void AuditFilter(EntityEntryEventArgs args)
    {
    }
}