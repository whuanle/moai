namespace MoAI.Workflow.Models;

/// <summary>
/// UI 设计模型，用于前端可视化设计器的布局和样式信息.
/// </summary>
public class UiDesign
{
    /// <summary>
    /// 节点位置信息，键为节点键，值为位置配置.
    /// </summary>
    public Dictionary<string, NodePosition> NodePositions { get; set; } = new();

    /// <summary>
    /// 画布缩放比例.
    /// </summary>
    public double Zoom { get; set; } = 1.0;

    /// <summary>
    /// 画布偏移量 X.
    /// </summary>
    public double OffsetX { get; set; } = 0;

    /// <summary>
    /// 画布偏移量 Y.
    /// </summary>
    public double OffsetY { get; set; } = 0;

    /// <summary>
    /// 其他自定义 UI 配置（扩展字段）.
    /// </summary>
    public Dictionary<string, object>? CustomSettings { get; set; }
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
    /// 节点宽度（可选）.
    /// </summary>
    public double? Width { get; set; }

    /// <summary>
    /// 节点高度（可选）.
    /// </summary>
    public double? Height { get; set; }
}
