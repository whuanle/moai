import { useEffect, useImperativeHandle, useRef, useState, type RefObject } from 'react'
import type { Graph as G6Graph, GraphData } from '@antv/g6'

/** G6 渲染层节点（样式已烘焙进 datum.style） */
export interface GraphRenderNode {
  id: string
  x: number
  y: number
  fill: string
  r: number
  labelText: string
}

/** G6 渲染层边 */
export interface GraphRenderEdge {
  id: string
  source: string
  target: string
  stroke: string
  labelText?: string
}

/** 画布视觉主题（取自 antd token，主题切换时由父组件 key 重挂载） */
export interface GraphCanvasTheme {
  background: string
  edgeStroke: string
  labelFill: string
  primary: string
}

/** 右键菜单目标 */
export interface ContextMenuTarget {
  kind: 'canvas' | 'node' | 'edge'
  id?: string
  clientX: number
  clientY: number
}

export interface GraphCanvasHandle {
  /** 全量替换渲染数据（含坐标）；options.fit 时渲染完成后自适应视口 */
  setData: (nodes: GraphRenderNode[], edges: GraphRenderEdge[], options?: { fit?: boolean }) => void
  /** 聚焦定位到指定节点 */
  focusNode: (id: string) => void
  /** 视口适配全部元素 */
  fitView: () => void
  /** 按比例缩放（>1 放大） */
  zoomBy: (ratio: number) => void
  /** 设置元素交互态（selected/highlight/expanded/dim） */
  setElementState: (id: string, states: Record<string, boolean>) => void
  /** 清除指定交互态（全部元素） */
  clearState: (state: string) => void
  /** 读取全部节点当前坐标（拖动后的真实位置，供布局连续性回写） */
  getPositions: () => Map<string, { x: number; y: number }>
}

interface GraphCanvasProps {
  theme: GraphCanvasTheme
  /** React19 ref 即 props；命令式句柄由父组件持有 */
  ref?: RefObject<GraphCanvasHandle | null>
  onNodeClick?: (id: string, ctrlKey: boolean) => void
  onNodeDoubleClick?: (id: string) => void
  onEdgeClick?: (id: string) => void
  onCanvasClick?: () => void
  onContextMenu?: (target: ContextMenuTarget) => void
}

/** 从 G6 事件对象提取元素 id（v5 元素事件 target.id，兼容历史字段） */
function extractElementId(event: unknown): string | undefined {
  const e = event as { target?: { id?: unknown }; itemId?: unknown }
  const id = e?.target?.id ?? e?.itemId
  return typeof id === 'string' ? id : undefined
}

/** 从 G6 事件对象提取原生鼠标事件（字段名随版本变化，逐一兜底） */
function extractNativeEvent(event: unknown): MouseEvent | undefined {
  const e = event as { originalEvent?: MouseEvent; nativeEvent?: MouseEvent; e?: MouseEvent }
  return e?.originalEvent ?? e?.nativeEvent ?? e?.e
}

/**
 * G6 v5 命令式画布封装：React19 下 useRef 持有 Graph 实例，
 * 布局坐标由父层自算后烘焙进 datum.style（规避 G6 布局配置差异，保留展开时位置连续）。
 * G6 以动态 import 加载（路由级代码分割；jsdom 等无 canvas 环境安全降级为空容器）。
 */
export function GraphCanvas(
  { theme, ref, onNodeClick, onNodeDoubleClick, onEdgeClick, onCanvasClick, onContextMenu }: GraphCanvasProps,
) {
  const containerRef = useRef<HTMLDivElement | null>(null)
  const graphRef = useRef<G6Graph | null>(null)
  const [failed, setFailed] = useState(false)
  const pendingRef = useRef<{ nodes: GraphRenderNode[]; edges: GraphRenderEdge[]; fit: boolean } | null>(null)

  // 事件处理器经 ref 转发，避免 Graph 实例因回调变化而重建
  const handlersRef = useRef({ onNodeClick, onNodeDoubleClick, onEdgeClick, onCanvasClick, onContextMenu })
  handlersRef.current = { onNodeClick, onNodeDoubleClick, onEdgeClick, onCanvasClick, onContextMenu }

  useEffect(() => {
    let disposed = false
    let observer: ResizeObserver | null = null
    void (async () => {
      try {
        const { Graph } = await import('@antv/g6')
        if (disposed || !containerRef.current) return
        const container = containerRef.current
        const graph = new Graph({
          container,
          width: container.clientWidth || 800,
          height: container.clientHeight || 600,
          animation: false,
          behaviors: ['drag-canvas', 'zoom-canvas', 'drag-element'],
          node: {
            type: 'circle',
            style: {
              labelPlacement: 'bottom',
              labelFontSize: 11,
              labelFill: theme.labelFill,
              labelBackground: false,
              cursor: 'pointer',
            },
            state: {
              selected: { stroke: theme.primary, lineWidth: 3 },
              highlight: { stroke: theme.primary, lineWidth: 3, halo: true },
              expanded: { lineWidth: 2, stroke: theme.primary },
              dim: { opacity: 0.15 },
            },
          },
          edge: {
            type: 'line',
            style: {
              endArrow: true,
              cursor: 'pointer',
            },
            state: {
              highlight: { stroke: theme.primary, lineWidth: 2.5 },
              dim: { opacity: 0.1 },
            },
          },
        })
        graphRef.current = graph

        graph.on('node:click', (event: unknown) => {
          const id = extractElementId(event)
          if (id) handlersRef.current.onNodeClick?.(id, extractNativeEvent(event)?.ctrlKey ?? false)
        })
        graph.on('node:dblclick', (event: unknown) => {
          const id = extractElementId(event)
          if (id) handlersRef.current.onNodeDoubleClick?.(id)
        })
        graph.on('edge:click', (event: unknown) => {
          const id = extractElementId(event)
          if (id) handlersRef.current.onEdgeClick?.(id)
        })
        graph.on('canvas:click', () => handlersRef.current.onCanvasClick?.())
        const emitContextMenu = (kind: ContextMenuTarget['kind']) => (event: unknown) => {
          const native = extractNativeEvent(event)
          const id = extractElementId(event)
          handlersRef.current.onContextMenu?.({ kind, id, clientX: native?.clientX ?? 0, clientY: native?.clientY ?? 0 })
        }
        graph.on('node:contextmenu', emitContextMenu('node'))
        graph.on('edge:contextmenu', emitContextMenu('edge'))
        graph.on('canvas:contextmenu', emitContextMenu('canvas'))

        // 容器尺寸变化同步画布
        observer = new ResizeObserver(() => {
          if (!containerRef.current) return
          graphRef.current?.setSize(containerRef.current.clientWidth || 800, containerRef.current.clientHeight || 600)
        })
        observer.observe(container)

        await graph.render()
        // 创建期间若已有待渲染数据（异步竞态：数据先到）则补放，fit 请求一并补做
        if (!disposed && pendingRef.current) {
          const pending = pendingRef.current
          pendingRef.current = null
          await applyData(graph, pending.nodes, pending.edges)
          if (pending.fit) await graph.fitView()
        }
      } catch {
        // jsdom/无 canvas 环境或加载失败：保持空容器，页面其余交互（图例/侧栏/工具栏）不受影响
        if (!disposed) setFailed(true)
      }
    })()
    return () => {
      disposed = true
      observer?.disconnect()
      graphRef.current?.destroy()
      graphRef.current = null
    }
    // 主题色由父组件 key={themeKey} 重挂载消化，此处仅初始化一次
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  useImperativeHandle(ref, () => ({
    setData: (nodes, edges, options) => {
      const graph = graphRef.current
      if (!graph) {
        pendingRef.current = { nodes, edges, fit: options?.fit ?? false }
        return
      }
      // 渲染完成后再 fit，避免对未完成的渲染边界计算视口
      void applyData(graph, nodes, edges).then(() => {
        if (options?.fit) return graph.fitView()
      })
    },
    focusNode: (id) => void graphRef.current?.focusElement(id),
    fitView: () => void graphRef.current?.fitView(),
    zoomBy: (ratio) => void graphRef.current?.zoomBy(ratio),
    setElementState: (id, states) => {
      const graph = graphRef.current
      if (!graph) return
      const active = Object.entries(states).filter(([, v]) => v).map(([k]) => k)
      const existing = graph.getElementState(id) ?? []
      // 保留未在本调用中声明的既有态（如 selected 与 expanded 并存）
      const next = Array.from(new Set([...existing.filter((s) => !(s in states)), ...active]))
      graph.setElementState({ [id]: next })
    },
    clearState: (state) => {
      const graph = graphRef.current
      if (!graph) return
      for (const datum of graph.getNodeData()) {
        if (datum.id) applyState(graph, datum.id, state, false)
      }
      for (const datum of graph.getEdgeData()) {
        if (datum.id) applyState(graph, datum.id, state, false)
      }
    },
    getPositions: () => {
      const out = new Map<string, { x: number; y: number }>()
      for (const datum of graphRef.current?.getNodeData() ?? []) {
        const id = typeof datum.id === 'string' ? datum.id : undefined
        const style = datum.style as { x?: unknown; y?: unknown } | undefined
        if (id && typeof style?.x === 'number' && typeof style?.y === 'number') {
          out.set(id, { x: style.x, y: style.y })
        }
      }
      return out
    },
  }), [])

  return (
    <div
      ref={containerRef}
      style={{ width: '100%', height: '100%', background: failed ? 'transparent' : theme.background }}
    />
  )
}

function applyData(graph: G6Graph, nodes: GraphRenderNode[], edges: GraphRenderEdge[]) {
  graph.setData({
    nodes: nodes.map((n) => ({
      id: n.id,
      style: {
        x: n.x,
        y: n.y,
        fill: n.fill,
        r: n.r,
        labelText: n.labelText,
      },
    })),
    edges: edges.map((e) => ({
      id: e.id,
      source: e.source,
      target: e.target,
      style: {
        stroke: e.stroke,
        endArrow: true,
        ...(e.labelText ? { labelText: e.labelText } : {}),
      },
    })),
  } as GraphData)
  // 返回渲染 promise：fitView 等依赖渲染边界的操作需在其完成后执行
  return graph.render()
}

function applyState(graph: G6Graph, id: string, state: string, value: boolean) {
  // v5 getElementState 返回态名数组；setElementState 支持「id → 态列表」的整体替换形态
  const existing = graph.getElementState(id) ?? []
  const next = value
    ? Array.from(new Set([...existing, state]))
    : existing.filter((x) => x !== state)
  graph.setElementState({ [id]: next })
}
