using System;
using System.Collections.Generic;
using MoAI.Database.Audits;

#pragma warning disable CA1051
#pragma warning disable SA1401
#pragma warning disable SA1600
#pragma warning disable SA1601
#pragma warning disable SA1204
namespace MoAI.Database.Entities;

public partial class KnowledgeGraphEntityTypeEntity : IFullAudited
{
    public long Id { get; set; }

    public long KnowledgeGraphId { get; set; }

    public string Name { get; set; } = default!;

    public string Color { get; set; } = default!;

    public string Description { get; set; } = default!;

    public int Sort { get; set; }

    public long IsDeleted { get; set; }

    public long CreateUserId { get; set; }

    public DateTimeOffset CreateTime { get; set; }

    public long UpdateUserId { get; set; }

    public DateTimeOffset UpdateTime { get; set; }
}
