import { forceCenter, forceCollide, forceLink, forceManyBody, forceSimulation, type SimulationLinkDatum, type SimulationNodeDatum } from 'd3-force'

/** 画布节点（渲染层数据） */
export interface CanvasNode {
  id: string
  label: string
  /** 托管图：实体类型 id 字符串；接入图：节点标签；用于着色与过滤 */
  typeKey: string
  entityTypeId?: number
  description?: string
  /** 实例属性值 */
  properties?: Record<string, string>
  /** 节点度数（后端画布/邻接查询携带；接入图为 0） */
  degree?: number
}

/** 画布边（渲染层数据） */
export interface CanvasEdge {
  id: string
  source: string
  target: string
  relationTypeId?: number
  /** 接入图直接带关系名；托管图按 relationTypeId 从模型解析 */
  relationName?: string
}

export interface LayoutPos { x: number; y: number }

export type LayoutKind = 'force' | 'circular' | 'grid'

/** 节点半径：度数越大越大（度数未知用基础值），夹在 12~26 */
export function nodeRadius(degree?: number): number {
  const d = Math.max(0, degree ?? 0)
  return Math.min(26, 12 + Math.sqrt(d) * 2)
}

/**
 * 力导向布局（d3-force 同步模拟）：老节点沿用上次坐标作初值保证展开时视觉连续，新节点环形落点后收敛。
 */
export function forceLayout(nodes: CanvasNode[], edges: CanvasEdge[], previous: Map<string, LayoutPos>): Map<string, LayoutPos> {
  interface SimNode extends SimulationNodeDatum { id: string }
  type SimLink = SimulationLinkDatum<SimNode>
  const simNodes: SimNode[] = nodes.map((n, i) => {
    const prev = previous.get(n.id)
    const angle = (i / Math.max(nodes.length, 1)) * Math.PI * 2
    return { id: n.id, x: prev?.x ?? Math.cos(angle) * 300, y: prev?.y ?? Math.sin(angle) * 300 }
  })
  const simLinks: SimLink[] = edges.filter((e) => e.source !== e.target).map((e) => ({ source: e.source, target: e.target }))
  forceSimulation(simNodes)
    .force('charge', forceManyBody().strength(-420))
    .force('link', forceLink<SimNode, SimLink>(simLinks).id((d) => d.id).distance(150).strength(0.4))
    .force('center', forceCenter(0, 0))
    .force('collide', forceCollide(56))
    .stop()
    .tick(280)
  const out = new Map<string, LayoutPos>()
  for (const n of simNodes) out.set(n.id, { x: n.x ?? 0, y: n.y ?? 0 })
  return out
}

/** 环形布局：按度数降序排座（重要节点靠前），保留位置作半径微调避免抖动 */
export function circularLayout(nodes: CanvasNode[], previous: Map<string, LayoutPos>): Map<string, LayoutPos> {
  const sorted = [...nodes].sort((a, b) => (b.degree ?? 0) - (a.degree ?? 0))
  const radius = Math.max(220, sorted.length * 14)
  const out = new Map<string, LayoutPos>()
  sorted.forEach((n, i) => {
    const angle = (i / Math.max(sorted.length, 1)) * Math.PI * 2
    out.set(n.id, { x: Math.cos(angle) * radius, y: Math.sin(angle) * radius })
  })
  void previous
  return out
}

/** 网格布局：按度数降序从中心螺旋向外（近平方矩形） */
export function gridLayout(nodes: CanvasNode[]): Map<string, LayoutPos> {
  const sorted = [...nodes].sort((a, b) => (b.degree ?? 0) - (a.degree ?? 0))
  const cols = Math.ceil(Math.sqrt(Math.max(sorted.length, 1)))
  const gap = 140
  const out = new Map<string, LayoutPos>()
  sorted.forEach((n, i) => {
    const row = Math.floor(i / cols)
    const col = i % cols
    out.set(n.id, { x: (col - (cols - 1) / 2) * gap, y: (row - (Math.ceil(sorted.length / cols) - 1) / 2) * gap })
  })
  return out
}

/** 按布局种类计算全部节点坐标 */
export function computeLayout(kind: LayoutKind, nodes: CanvasNode[], edges: CanvasEdge[], previous: Map<string, LayoutPos>): Map<string, LayoutPos> {
  if (kind === 'circular') return circularLayout(nodes, previous)
  if (kind === 'grid') return gridLayout(nodes)
  return forceLayout(nodes, edges, previous)
}
