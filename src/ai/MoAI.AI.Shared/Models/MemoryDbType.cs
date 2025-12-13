using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoAI.AI.Models;

public enum MemoryDbType
{
    ElasticSearch = 1,
    Qdrant = 2,
    Postgres = 3,
    Redis = 4,
    SQLServer = 5
}
