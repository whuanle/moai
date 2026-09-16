/**
 * 编辑器 JSON（FlowGram 画布）与引擎 WorkflowDefinition（nodes + connections DAG）的双向转换，
 * 以及与后端验证器同构的客户端快速校验.
 */

import { generateNodeId, getNodeTemplate, isSupportedNodeType, NODE_CONSTRAINTS } from './constants'
import type {
  EditorEdgeJSON,
  EditorNodeJSON,
  EditorWorkflowJSON,
  FieldBinding,
  NodeSettings,
  NodeType,
  OutputField,
  WorkflowConnectionDef,
  WorkflowDefinition,
  WorkflowNodeDef,
} from './types'

/** 引擎契约中条件节点出边的合法 condition 值 */
const CONDITION_VALUES = ['true', 'false']

/** 空画布默认编排：start → end */
export function createDefaultEditorData(): EditorWorkflowJSON {
  const startId = generateNodeId('start')
  const endId = generateNodeId('end')
  return {
    nodes: [
      { id: startId, type: 'start', meta: { position: { x: 120, y: 220 } }, data: nodeDataFromTemplate('start'), blocks: [], edges: [{ sourceNodeID: startId, targetNodeID: endId }] },
      { id: endId, type: 'end', meta: { position: { x: 520, y: 220 } }, data: nodeDataFromTemplate('end'), blocks: [], edges: [] },
    ],
    edges: [],
  }
}

/** 按模板生成节点表单数据（拖拽新增/默认画布共用） */
export function nodeDataFromTemplate(type: NodeType | string): EditorNodeJSON['data'] {
  const template = getNodeTemplate(type)
  return {
    title: template?.name ?? type,
    content: template?.desc ?? '',
    inputs: template ? JSON.parse(JSON.stringify(template.inputs)) : {},
    outputs: template ? JSON.parse(JSON.stringify(template.outputs)) : [],
    settings: template?.settings ? { ...template.settings } : {},
  }
}

/**
 * 引擎定义 → 编辑器画布 JSON（加载草稿时调用）.
 * definition 为空时返回默认画布.
 */
export function toEditorFormat(definition: WorkflowDefinition | null | undefined): EditorWorkflowJSON {
  if (!definition || !Array.isArray(definition.nodes) || definition.nodes.length === 0) {
    return createDefaultEditorData()
  }

  // 引擎 connections 是扁平表；FlowGram 的边挂在源节点下
  const edgesBySource = new Map<string, EditorEdgeJSON[]>()
  for (const conn of definition.connections ?? []) {
    const list = edgesBySource.get(conn.source) ?? []
    const sourceNode = definition.nodes.find((n) => n.key === conn.source)
    list.push({
      sourceNodeID: conn.source,
      targetNodeID: conn.target,
      ...(sourceNode?.type === 'condition' && conn.condition ? { sourcePortID: conn.condition } : {}),
    })
    edgesBySource.set(conn.source, list)
  }

  const nodes: EditorNodeJSON[] = definition.nodes
    .filter((n) => isSupportedNodeType(n.type))
    .map((n) => ({
      id: n.key,
      type: n.type,
      meta: {
        position: definition.ui?.nodePositions?.[n.key] ?? { x: 120, y: 220 },
        defaultExpanded: true,
      },
      data: {
        title: n.name,
        content: n.description ?? '',
        inputs: n.inputs ?? {},
        outputs: n.outputs ?? [],
        settings: n.config ?? {},
      },
      blocks: [],
      edges: edgesBySource.get(n.key) ?? [],
    }))

  return { nodes, edges: [] }
}

/**
 * 编辑器画布 JSON → 引擎定义（保存/调试执行时调用）.
 * 以画布为唯一数据源：节点表单数据 + 出边即完整编排.
 */
export function fromEditorFormat(editor: EditorWorkflowJSON, name: string, description?: string): WorkflowDefinition {
  const nodes: WorkflowNodeDef[] = (editor.nodes ?? [])
    .filter((n) => isSupportedNodeType(String(n.type)))
    .map((n) => ({
      key: n.id,
      name: String(n.data?.title ?? n.id),
      type: String(n.type) as NodeType,
      description: n.data?.content || undefined,
      config: sanitizeSettings(n.data?.settings),
      inputs: sanitizeInputs(n.data?.inputs),
      outputs: sanitizeOutputs(n.data?.outputs),
    }))

  const nodeIds = new Set(nodes.map((n) => n.key))

  // 从各节点的 edges 提取全量连接（顶层 edges 在自由布局里恒为空，兜底合并）
  const seen = new Set<string>()
  const connections: WorkflowConnectionDef[] = []
  const pushConnection = (edge: EditorEdgeJSON, sourceType: string) => {
    const id = `c_${edge.sourceNodeID}_${edge.targetNodeID}${edge.sourcePortID ? `_${edge.sourcePortID}` : ''}`
    if (seen.has(id) || !nodeIds.has(edge.sourceNodeID) || !nodeIds.has(edge.targetNodeID)) return
    seen.add(id)
    connections.push({
      id,
      source: edge.sourceNodeID,
      target: edge.targetNodeID,
      ...(sourceType === 'condition' && CONDITION_VALUES.includes(String(edge.sourcePortID))
        ? { condition: String(edge.sourcePortID), label: String(edge.sourcePortID) === 'true' ? '满足' : '不满足' }
        : {}),
    })
  }

  for (const node of editor.nodes ?? []) {
    const sourceType = String(node.type)
    for (const edge of node.edges ?? []) {
      pushConnection(edge, sourceType)
    }
  }
  for (const edge of editor.edges ?? []) {
    pushConnection(edge, String(editor.nodes?.find((n) => n.id === edge.sourceNodeID)?.type ?? ''))
  }

  const nodePositions: Record<string, { x: number; y: number }> = {}
  for (const node of editor.nodes ?? []) {
    if (node.meta?.position) {
      nodePositions[node.id] = { x: node.meta.position.x, y: node.meta.position.y }
    }
  }

  return {
    id: '',
    name,
    description,
    version: 0,
    status: 'draft',
    nodes,
    connections,
    ui: { nodePositions },
  }
}

/** 清洗节点私有配置：仅保留引擎认识的键 */
function sanitizeSettings(settings: Record<string, unknown> | NodeSettings | undefined): Record<string, unknown> {
  const result: Record<string, unknown> = {}
  if (!settings) return result
  for (const key of ['aiModelId', 'pluginKey', 'code'] as const) {
    const value = settings[key]
    if (typeof value === 'string' && value !== '') {
      result[key] = value
    }
  }
  return result
}

/** 清洗输入绑定：剔除无效字段与空引用 */
function sanitizeInputs(inputs: Record<string, FieldBinding> | undefined): Record<string, FieldBinding> {
  const result: Record<string, FieldBinding> = {}
  if (!inputs) return result
  for (const [fieldName, binding] of Object.entries(inputs)) {
    if (!binding || typeof binding !== 'object') continue
    const expressionType = String(binding.expressionType)
    if (!['run', 'fixed', 'variable', 'jsonpath', 'interpolation'].includes(expressionType)) continue
    result[fieldName] = {
      expressionType: expressionType as FieldBinding['expressionType'],
      value: String(binding.value ?? ''),
      required: binding.required !== false,
      ...(binding.description ? { description: binding.description } : {}),
    }
  }
  return result
}

/** 清洗输出字段 */
function sanitizeOutputs(outputs: OutputField[] | undefined): OutputField[] {
  if (!Array.isArray(outputs)) return []
  return outputs
    .filter((o) => o && typeof o.name === 'string' && o.name !== '')
    .map((o) => ({
      name: o.name,
      fieldType: String(o.fieldType ?? 'dynamic'),
      isRequired: o.isRequired === true,
      ...(o.description ? { description: o.description } : {}),
    }))
}

// ==================== 客户端校验（与引擎 WorkflowValidator 同构的快速反馈） ====================

export interface ValidationError {
  nodeId?: string
  message: string
}

export function validateEditorData(editor: EditorWorkflowJSON | null): ValidationError[] {
  const errors: ValidationError[] = []
  if (!editor) return errors

  const nodes = (editor.nodes ?? []).filter((n) => isSupportedNodeType(String(n.type)))
  if (nodes.length === 0) {
    errors.push({ message: '流程必须包含至少一个节点' })
    return errors
  }

  const ids = nodes.map((n) => n.id)
  const idSet = new Set(ids)
  if (idSet.size !== ids.length) {
    errors.push({ message: '存在重复的节点 id' })
  }

  const startNodes = nodes.filter((n) => n.type === 'start')
  const endNodes = nodes.filter((n) => n.type === 'end')
  if (startNodes.length !== 1) errors.push({ message: '流程必须恰好包含一个开始节点' })
  if (endNodes.length === 0) errors.push({ message: '流程必须包含至少一个结束节点' })

  // 出边表
  const outgoing = new Map<string, EditorEdgeJSON[]>()
  const incoming = new Map<string, number>()
  for (const node of nodes) {
    const edges = (node.edges ?? []).filter((e) => idSet.has(e.targetNodeID) && e.targetNodeID !== node.id)
    outgoing.set(node.id, edges)
    for (const e of edges) incoming.set(e.targetNodeID, (incoming.get(e.targetNodeID) ?? 0) + 1)
  }

  for (const node of nodes) {
    const constraints = NODE_CONSTRAINTS[String(node.type) as NodeType]
    const outs = outgoing.get(node.id) ?? []
    if (constraints.requiresInput && (incoming.get(node.id) ?? 0) === 0) {
      errors.push({ nodeId: node.id, message: `节点「${node.data?.title ?? node.id}」缺少输入连接` })
    }
    if (constraints.requiresOutput && outs.length === 0) {
      errors.push({ nodeId: node.id, message: `节点「${node.data?.title ?? node.id}」缺少输出连接` })
    }

    // 条件节点：恰好 true/false 各一条出边
    if (node.type === 'condition') {
      const conditions = outs.map((e) => String(e.sourcePortID))
      for (const value of CONDITION_VALUES) {
        if (!conditions.includes(value)) {
          errors.push({ nodeId: node.id, message: `条件节点「${node.data?.title ?? node.id}」缺少 ${value} 分支出边` })
        }
      }
    }
  }

  // 环检测（DFS 三色）
  if (startNodes.length === 1 && hasCycle(nodes, outgoing)) {
    errors.push({ message: '流程存在环路，不允许循环连接' })
  }

  // 变量引用必须是上游节点
  for (const node of nodes) {
    const ancestors = collectAncestors(node.id, outgoing)
    for (const [fieldName, binding] of Object.entries(node.data?.inputs ?? {})) {
      if (binding?.expressionType !== 'variable' || !binding.value) continue
      const prefix = binding.value.split('.')[0]
      if (!prefix || prefix === 'sys' || prefix === 'input') continue
      if (!idSet.has(prefix)) {
        errors.push({ nodeId: node.id, message: `输入 ${fieldName} 引用了不存在的节点：${binding.value}` })
      } else if (!ancestors.has(prefix)) {
        errors.push({ nodeId: node.id, message: `输入 ${fieldName} 引用了非上游节点：${binding.value}` })
      }
    }
  }

  return errors
}

function hasCycle(nodes: EditorNodeJSON[], outgoing: Map<string, EditorEdgeJSON[]>): boolean {
  const state = new Map<string, number>()
  const visit = (id: string): boolean => {
    state.set(id, 1)
    for (const edge of outgoing.get(id) ?? []) {
      const next = state.get(edge.targetNodeID) ?? 0
      if (next === 1 || (next === 0 && visit(edge.targetNodeID))) return true
    }
    state.set(id, 2)
    return false
  }
  for (const node of nodes) {
    if ((state.get(node.id) ?? 0) === 0 && visit(node.id)) return true
  }
  return false
}

function collectAncestors(nodeId: string, outgoing: Map<string, EditorEdgeJSON[]>): Set<string> {
  const incoming = new Map<string, string[]>()
  for (const [source, edges] of outgoing) {
    for (const edge of edges) {
      const list = incoming.get(edge.targetNodeID) ?? []
      list.push(source)
      incoming.set(edge.targetNodeID, list)
    }
  }
  const ancestors = new Set<string>()
  const queue = [...(incoming.get(nodeId) ?? [])]
  while (queue.length > 0) {
    const current = queue.shift() as string
    if (ancestors.has(current)) continue
    ancestors.add(current)
    queue.push(...(incoming.get(current) ?? []))
  }
  return ancestors
}
