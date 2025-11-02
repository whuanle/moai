namespace MoAI.Database.Audits;

/// <summary>
/// 全部审计属性.
/// </summary>
public interface IFullAudited : ICreationAudited, IDeleteAudited
{
}
