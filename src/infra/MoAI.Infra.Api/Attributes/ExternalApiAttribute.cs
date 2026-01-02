using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MoAI.Infra.Attributes;

/// <summary>
/// 外部接口.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
[DebuggerDisplay("{ToString(),nq}")]
public class ExternalApiAttribute : Attribute
{
}
