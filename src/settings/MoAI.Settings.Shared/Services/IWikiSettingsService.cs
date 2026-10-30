namespace MoAI.Settings.Services;

/// <summary>
/// 知识库设置读取服务，供业务模块感知知识库上传约束.
/// </summary>
public interface IWikiSettingsService
{
    /// <summary>
    /// 获取知识库上传文件大小上限（MB），0 表示不限制（仅受平台硬上限约束）.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回上限（MB），非正数归一为 0.</returns>
    Task<int> GetMaxFileSizeMbAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 获取知识库允许上传的文件类型（归一为小写扩展名，含点号），网页端与开放接口上传共用同一份白名单.
    /// </summary>
    /// <param name="cancellationToken">取消令牌.</param>
    /// <returns>返回扩展名列表；设置缺失、为空或全部非法时回退内置默认值.</returns>
    Task<IReadOnlyList<string>> GetAllowedFileTypesAsync(CancellationToken cancellationToken);
}
