namespace MoAI.Account.Services;

/// <summary>
/// 用户注册增强钩子：账号创建成功后追加附加动作（如赋予组织默认角色）.
/// 实现**只往当前 DbContext 入列、不得调用 SaveChanges**，由注册主流程统一提交；
/// 应自行吞异常保证注册不受附加动作影响.
/// </summary>
public interface IUserRegistrationHook
{
    /// <summary>
    /// 用户注册完成后调用（用户 id 已生成、用户实体已落库）.
    /// </summary>
    /// <param name="userId">新用户 id.</param>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>异步任务.</returns>
    Task OnUserRegisteredAsync(long userId, CancellationToken cancellationToken);
}
