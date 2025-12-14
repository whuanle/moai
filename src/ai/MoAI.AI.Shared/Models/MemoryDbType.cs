using System.Text.Json.Serialization;

namespace MoAI.AI.Models;

public enum MemoryDbType
{
    ElasticSearch = 1,
    Qdrant = 2,
    Postgres = 3,
    Redis = 4,
    SQLServer = 5
}
