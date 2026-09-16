using System.Text.Json.Serialization;

namespace MoAI.App.Workflow.Definition;

/// <summary>
/// 工作流定义模型 - 前端设计器输出的完整 JSON，是整个引擎的输入契约.
/// 分为流程定义（本模型）、流程实例（Instance）、流程数据传输（DataTransfer）三部分协作.
/// </summary>
public class WorkflowDefinition
{
    /// <summary>
    /// 工作流定义唯一标识符.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 工作流名称.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 工作流描述.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    /// <summary>
    /// 版本号，发布后不可变，执行实例记录其引用的版本.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// 定义状态.
    /// </summary>
    public DefinitionStatus Status { get; set; } = DefinitionStatus.Draft;

    /// <summary>
    /// 节点列表.
    /// </summary>
    public List<NodeDefinition> Nodes { get; set; } = new();

    /// <summary>
    /// 节点连接（控制流）列表.
    /// </summary>
    public List<ConnectionDefinition> Connections { get; set; } = new();

    /// <summary>
    /// 前端设计器画布布局信息（节点坐标、缩放等），引擎不使用，仅随定义一起存储.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public UiDesign? Ui { get; set; }
}

/// <summary>
/// UI 设计模型，用于前端可视化设计器的布局信息.
/// </summary>
public class UiDesign
{
    /// <summary>
    /// 节点位置信息，键为节点 Key.
    /// </summary>
    public Dictionary<string, NodePosition> NodePositions { get; set; } = new();

    /// <summary>
    /// 画布缩放比例.
    /// </summary>
    public double Zoom { get; set; } = 1.0;

    /// <summary>
    /// 画布偏移量 X.
    /// </summary>
    public double OffsetX { get; set; }

    /// <summary>
    /// 画布偏移量 Y.
    /// </summary>
    public double OffsetY { get; set; }
}

/// <summary>
/// 节点位置信息.
/// </summary>
public class NodePosition
{
    /// <summary>
    /// X 坐标.
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Y 坐标.
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// 节点宽度.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Width { get; set; }

    /// <summary>
    /// 节点高度.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Height { get; set; }
}
