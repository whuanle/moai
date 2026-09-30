using System.Collections.Generic;
using System.ComponentModel;

namespace MoAI.AIPlugin.Dynamic.Models;

/// <summary>
/// Kubernetes 查询插件配置（每个实例独立保存，只读 GET）.
/// </summary>
public class KubernetesQueryConfig
{
    /// <summary>
    /// API Server 地址.
    /// </summary>
    [Description("Kubernetes API Server 地址，例如 https://192.168.1.10:6443 或集群内 https://kubernetes.default.svc")]
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Bearer 令牌.
    /// </summary>
    [Description("Bearer 令牌（ServiceAccount JWT 或用户令牌）；建议使用只读权限的 ServiceAccount")]
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// 是否跳过 TLS 证书校验.
    /// </summary>
    [Description("是否跳过 TLS 证书校验（API Server 证书多为私有 CA 签发，集群外访问保持默认 true；false 时要求受信证书链）")]
    public bool SkipTlsVerify { get; set; } = true;

    /// <summary>
    /// 单次请求超时秒数（1-300）.
    /// </summary>
    [Description("单次请求超时秒数，取值 1-300（默认 30）")]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// 列表最多返回条数（1-500）.
    /// </summary>
    [Description("pods/events/deployments/nodes 等列表最多返回条数，取值 1-500（默认 100）；超出截断（不翻页）并把 Truncated 置为 true")]
    public int MaxListItems { get; set; } = 100;

    /// <summary>
    /// Pod 日志最大字符数（1024-131072）.
    /// </summary>
    [Description("pod_logs 模式返回的日志最大字符数，取值 1024-131072（默认 16384），超出截断")]
    public int MaxLogChars { get; set; } = 16384;
}

/// <summary>
/// Kubernetes 查询插件请求参数.
/// </summary>
public class KubernetesQueryRequest
{
    /// <summary>
    /// 操作模式.
    /// </summary>
    [Description("操作模式：pods=Pod 列表（默认）/ pod_logs=Pod 日志 / events=事件 / deployments=Deployment 列表 / nodes=节点列表；未知值会被拒绝")]
    public string Mode { get; set; } = "pods";

    /// <summary>
    /// 命名空间（可空=全部）.
    /// </summary>
    [Description("命名空间（可空=全部命名空间）；pods/events/deployments/pod_logs 生效")]
    public string? Namespace { get; set; }

    /// <summary>
    /// 标签选择器.
    /// </summary>
    [Description("pods 模式：标签选择器 labelSelector，例如 app=nginx")]
    public string? LabelSelector { get; set; }

    /// <summary>
    /// pod_logs 模式：目标 Pod 名.
    /// </summary>
    [Description("pod_logs 模式：目标 Pod 名（必填，配合 Namespace）")]
    public string? Pod { get; set; }

    /// <summary>
    /// pod_logs 模式：容器名.
    /// </summary>
    [Description("pod_logs 模式：容器名（多容器 Pod 必填，单容器可空）")]
    public string? Container { get; set; }

    /// <summary>
    /// pod_logs 模式：尾部行数.
    /// </summary>
    [Description("pod_logs 模式：尾部行数 tailLines，1-1000（默认 200）")]
    public int TailLines { get; set; } = 200;

    /// <summary>
    /// pod_logs 模式：是否取上一次崩溃容器日志.
    /// </summary>
    [Description("pod_logs 模式：是否取上一次崩溃容器的日志（previous=true，排查 CrashLoopBackOff 用）")]
    public bool Previous { get; set; }
}

/// <summary>
/// Kubernetes 查询插件响应.
/// </summary>
public class KubernetesQueryResponse
{
    /// <summary>
    /// 结果类型：pods / pod_logs / events / deployments / nodes.
    /// </summary>
    public string ResultType { get; set; } = string.Empty;

    /// <summary>
    /// pods 模式：Pod 列表.
    /// </summary>
    public IReadOnlyList<KubernetesPod> Pods { get; set; } = new List<KubernetesPod>();

    /// <summary>
    /// pod_logs 模式：日志文本（按 MaxLogChars 截断）.
    /// </summary>
    public string? LogText { get; set; }

    /// <summary>
    /// pod_logs 模式：实际读取的容器.
    /// </summary>
    public string? Container { get; set; }

    /// <summary>
    /// events 模式：事件列表.
    /// </summary>
    public IReadOnlyList<KubernetesEvent> Events { get; set; } = new List<KubernetesEvent>();

    /// <summary>
    /// deployments 模式：Deployment 列表.
    /// </summary>
    public IReadOnlyList<KubernetesDeployment> Deployments { get; set; } = new List<KubernetesDeployment>();

    /// <summary>
    /// nodes 模式：节点列表.
    /// </summary>
    public IReadOnlyList<KubernetesNode> Nodes { get; set; } = new List<KubernetesNode>();

    /// <summary>
    /// 列表是否因超过 MaxListItems 被截断.
    /// </summary>
    public bool Truncated { get; set; }
}

/// <summary>
/// Kubernetes Pod.
/// </summary>
public class KubernetesPod
{
    /// <summary>
    /// Pod 名.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 命名空间.
    /// </summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// 阶段（Running/Pending/Succeeded/Failed/Unknown）.
    /// </summary>
    public string Phase { get; set; } = string.Empty;

    /// <summary>
    /// Pod IP.
    /// </summary>
    public string PodIp { get; set; } = string.Empty;

    /// <summary>
    /// 所在节点.
    /// </summary>
    public string NodeName { get; set; } = string.Empty;

    /// <summary>
    /// 就绪容器数（如 1/2）.
    /// </summary>
    public string ReadyContainers { get; set; } = string.Empty;

    /// <summary>
    /// 重启次数合计.
    /// </summary>
    public int RestartCount { get; set; }

    /// <summary>
    /// 创建时间（ISO 8601 UTC）.
    /// </summary>
    public string CreatedAt { get; set; } = string.Empty;

    /// <summary>
    /// 标签.
    /// </summary>
    public IReadOnlyDictionary<string, string> Labels { get; set; } = new Dictionary<string, string>();
}

/// <summary>
/// Kubernetes 事件.
/// </summary>
public class KubernetesEvent
{
    /// <summary>
    /// 类型（Warning/Normal）.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// 原因（如 FailedScheduling、BackOff）.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// 消息.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 关联对象类型.
    /// </summary>
    public string ObjectKind { get; set; } = string.Empty;

    /// <summary>
    /// 关联对象名.
    /// </summary>
    public string ObjectName { get; set; } = string.Empty;

    /// <summary>
    /// 关联对象命名空间.
    /// </summary>
    public string ObjectNamespace { get; set; } = string.Empty;

    /// <summary>
    /// 最近一次发生时间（ISO 8601 UTC）.
    /// </summary>
    public string LastTimestamp { get; set; } = string.Empty;

    /// <summary>
    /// 发生次数.
    /// </summary>
    public int Count { get; set; }
}

/// <summary>
/// Kubernetes Deployment.
/// </summary>
public class KubernetesDeployment
{
    /// <summary>
    /// 名称.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 命名空间.
    /// </summary>
    public string Namespace { get; set; } = string.Empty;

    /// <summary>
    /// 期望副本数.
    /// </summary>
    public int Replicas { get; set; }

    /// <summary>
    /// 就绪副本数.
    /// </summary>
    public int ReadyReplicas { get; set; }

    /// <summary>
    /// 可用副本数.
    /// </summary>
    public int AvailableReplicas { get; set; }

    /// <summary>
    /// 容器镜像列表.
    /// </summary>
    public IReadOnlyList<string> Images { get; set; } = new List<string>();

    /// <summary>
    /// 创建时间（ISO 8601 UTC）.
    /// </summary>
    public string CreatedAt { get; set; } = string.Empty;
}

/// <summary>
/// Kubernetes 节点.
/// </summary>
public class KubernetesNode
{
    /// <summary>
    /// 节点名.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 是否就绪（Ready 条件）.
    /// </summary>
    public bool Ready { get; set; }

    /// <summary>
    /// kubelet 版本.
    /// </summary>
    public string KubeletVersion { get; set; } = string.Empty;

    /// <summary>
    /// 可分配 CPU（核）.
    /// </summary>
    public string Cpu { get; set; } = string.Empty;

    /// <summary>
    /// 可分配内存（原值，如 7976Mi）.
    /// </summary>
    public string Memory { get; set; } = string.Empty;

    /// <summary>
    /// 内部 IP.
    /// </summary>
    public string InternalIp { get; set; } = string.Empty;
}
