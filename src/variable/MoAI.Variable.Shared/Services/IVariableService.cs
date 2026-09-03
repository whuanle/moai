namespace MoAI.Variable.Services;

/// <summary>
/// 变量服务，供插件运行时等在服务端内部做 <c>${key}</c> 替换（含私密变量解密）；
/// 替换结果可能包含私密值，禁止通过面向成员的接口返回.
/// </summary>
public interface IVariableService
{
    /// <summary>
    /// 将文本中的 <c>${key}</c> 替换为团队变量值（私密变量解密）；未匹配到变量的占位符保留原文.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="content">待替换文本.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回替换后的文本.</returns>
    Task<string> SubstituteAsync(long teamId, string content, CancellationToken cancellationToken = default);
}
