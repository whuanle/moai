using MoAI.Database.Entities;

namespace MoAI.Message.Tests;

/// <summary>
/// 站内信单测种子数据构造.
/// </summary>
internal static class MessageTestSeed
{
    /// <summary>
    /// 构造站内信.
    /// </summary>
    /// <param name="recipientUserId">接收人用户 id.</param>
    /// <param name="title">标题.</param>
    /// <param name="isRead">是否已读.</param>
    /// <param name="messageType">消息类型（int 值）.</param>
    /// <param name="source">来源模块标识.</param>
    /// <returns>站内信实体.</returns>
    public static MessageEntity Message(long recipientUserId, string title = "标题", bool isRead = false, int messageType = 1, string source = "test") => new()
    {
        RecipientUserId = recipientUserId,
        Title = title,
        Content = "正文内容",
        MessageType = messageType,
        Source = source,
        SenderUserId = null,
        IsRead = isRead,
    };

    /// <summary>
    /// 构造用户（字符串列全填，sqlite 非空约束兜底）.
    /// </summary>
    /// <param name="id">用户 id.</param>
    /// <param name="userName">用户名.</param>
    /// <param name="isDisable">是否禁用.</param>
    /// <returns>用户实体.</returns>
    public static UserEntity User(long id, string userName, bool isDisable = false) => new()
    {
        Id = id,
        UserName = userName,
        NickName = userName,
        Email = $"{userName}@test.local",
        Password = string.Empty,
        PasswordSalt = string.Empty,
        AvatarPath = string.Empty,
        Phone = string.Empty,
        IsDisable = isDisable,
    };

    /// <summary>
    /// 构造团队.
    /// </summary>
    /// <param name="id">团队 id.</param>
    /// <param name="name">团队名称.</param>
    /// <returns>团队实体.</returns>
    public static TeamEntity Team(int id, string name) => new()
    {
        Id = id,
        Name = name,
        Description = string.Empty,
        AvatarPath = string.Empty,
    };

    /// <summary>
    /// 构造团队成员绑定.
    /// </summary>
    /// <param name="teamId">团队 id.</param>
    /// <param name="userId">用户 id.</param>
    /// <returns>成员绑定实体.</returns>
    public static TeamUserEntity TeamUser(int teamId, long userId) => new()
    {
        TeamId = teamId,
        UserId = userId,
    };
}
