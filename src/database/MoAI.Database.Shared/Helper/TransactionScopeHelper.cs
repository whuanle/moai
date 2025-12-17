using System.Transactions;

namespace MoAI.Database.Helper;

/// <summary>
/// 创建事务.
/// </summary>
public static class TransactionScopeHelper
{
    /// <summary>
    /// 创建事务.
    /// </summary>
    /// <param name="isolationLevel"></param>
    /// <returns></returns>
    public static TransactionScope Create(IsolationLevel isolationLevel = IsolationLevel.RepeatableRead)
    {
        return new TransactionScope(
            scopeOption: TransactionScopeOption.Required,
            asyncFlowOption: TransactionScopeAsyncFlowOption.Enabled,
            transactionOptions: new TransactionOptions { IsolationLevel = isolationLevel });
    }
}
