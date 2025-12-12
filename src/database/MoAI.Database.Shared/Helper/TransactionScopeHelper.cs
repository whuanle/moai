using System.Transactions;

namespace MoAI.Database.Helper;

public static class TransactionScopeHelper
{
    public static TransactionScope Create(IsolationLevel isolationLevel = IsolationLevel.RepeatableRead)
    {
        return new TransactionScope(
            scopeOption: TransactionScopeOption.Required,
            asyncFlowOption: TransactionScopeAsyncFlowOption.Enabled,
            transactionOptions: new TransactionOptions { IsolationLevel = isolationLevel });
    }
}
