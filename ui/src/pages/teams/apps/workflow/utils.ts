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

/**
 * 汇总画布全部连线（两处来源合并去重）：
 * 画布上画的连线由 document.toJSON() 序列化在顶层 edges，加载进去的连线在节点内 edges.
 */
export function collectEdges(editor: EditorWorkflowJSON | null): EditorEdgeJSON[] {
  if (!editor) return []
  const result: EditorEdgeJSON[] = []
  const seen = new Set<string>()
  const push = (edge: EditorEdgeJSON) => {
    const key = `${edge.sourceNodeID}>${edge.targetNodeID}>${edge.sourcePortID ?? ''}`
    if (seen.has(key)) return
    seen.add(key)
    result.push(edge)
  }
  for (const node of editor.nodes ?? []) {
    for (const edge of node.edges ?? []) push(edge)
  }
  for (const edge of editor.edges ?? []) push(edge)
  return result
}

/**
 * 规范化编辑器 JSON：把节点内 edges 全部提升到顶层 edges.
 * FlowGram 加载节点内出边时按节点顺序即时创建，若目标节点排在后面会被静默丢弃；
 * 顶层 edges 则在全部节点创建完后统一创建，两种历史格式都规范化到顶层以保证鲁棒.
 */
export function normalizeEditorData(editor: EditorWorkflowJSON): EditorWorkflowJSON {
  const nodes = (editor.nodes ?? []).map((node) => {
    const { edges: _ignoredEdges, ...rest } = node
    void _ignoredEdges
    return rest as EditorNodeJSON
  })
  return { nodes, edges: collectEdges(editor) }
}

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
  if (type === 'switch') {
    return {
      title: '多条件',
      content: template?.desc ?? '',
      branches: [
        { id: 'b1', label: '条件 1', binding: { expressionType: 'variable', value: '', required: true } },
        { id: 'b2', label: '条件 2', binding: { expressionType: 'variable', value: '', required: true } },
      ],
      inputs: {},
      outputs: [],
      settings: {},
    }
  }

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

  // 引擎 connections 是扁平表；放入顶层 edges（与画布上画的连线序列化位置一致）
  const edges: EditorEdgeJSON[] = []
  for (const conn of definition.connections ?? []) {
    const sourceNode = definition.nodes.find((n) => n.key === conn.source)
    edges.push({
      sourceNodeID: conn.source,
      targetNodeID: conn.target,
      ...((sourceNode?.type === 'condition' || sourceNode?.type === 'switch') && conn.condition
        ? { sourcePortID: conn.condition }
        : {}),
    })
  }

  const nodes: EditorNodeJSON[] = definition.nodes
    .filter((n) => isSupportedNodeType(n.type))
    .map((n) => {
      // 开始节点：声明的输出即启动参数，加载时映射回 data.inputs 供「输入参数」编辑器回显
      const startInputs =
        n.type === 'start'
          ? (n.outputs ?? []).reduce<Record<string, FieldBinding>>((acc, o) => {
              acc[o.name] = {
                expressionType: 'run',
                value: '',
                required: o.isRequired !== false,
                fieldType: o.fieldType,
                ...(o.description ? { description: o.description } : {}),
              }
              return acc
            }, {})
          : undefined

      // switch：config.branches 还原为 data.branches 供分支编辑器展示
      const configObj = (n.config ?? {}) as Record<string, unknown>
      const branches =
        n.type === 'switch' && Array.isArray(configObj.branches)
          ? (configObj.branches as Record<string, unknown>[]).map((b, i) => ({
              id: String(b.id ?? `b${i + 1}`),
              label: String(b.label ?? `条件 ${i + 1}`),
              binding: (b.binding ?? {}) as FieldBinding,
            }))
          : undefined

      return {
        id: n.key,
        type: n.type,
        meta: {
          position: definition.ui?.nodePositions?.[n.key] ?? { x: 120, y: 220 },
          defaultExpanded: true,
        },
        data: {
          title: n.name,
          content: n.description ?? '',
          branches,
          inputs: startInputs ?? n.inputs ?? {},
          outputs: n.type === 'start' ? [] : (n.outputs ?? []),
          settings: n.type === 'switch' ? {} : (n.config ?? {}),
        },
        blocks: [],
        edges: [],
      }
    })

  return { nodes, edges }
}

/** 有效节点 key：data.key 自定义覆盖优先，否则用画布 id（id 是画布内部标识，key 是引擎/引用前缀） */
export function nodeKeyOf(n: EditorNodeJSON): string {
  const custom = String(n.data?.key ?? '').trim()
  return custom || n.id
}

/** 引用重写：variable/jsonpath 的 `旧id.` 前缀与 interpolation 的 `{旧id.` 花括号换成新 key */
function rewriteRefValue(value: string, idToKey: Map<string, string>): string {
  let out = value
  for (const [oldId, key] of idToKey) {
    if (oldId === key) continue
    if (out.startsWith(`${oldId}.`)) out = `${key}${out.slice(oldId.length)}`
    out = out.split(`{${oldId}.`).join(`{${key}.`)
  }
  return out
}

/**
 * 画布引用规范化（加载草稿时调用）：节点声明了 data.key 后，画布快照里的引用可能还是旧 id
 * 前缀（保存时只重写了引擎定义），这里统一改成有效 key，保证画布显示与 Key 一致.
 */
export function normalizeReferences(editor: EditorWorkflowJSON): EditorWorkflowJSON {
  const idToKey = new Map<string, string>()
  for (const n of editor.nodes ?? []) {
    const key = nodeKeyOf(n)
    if (key !== n.id) idToKey.set(n.id, key)
  }
  if (idToKey.size === 0) return editor
  const nodes = (editor.nodes ?? []).map((n) => {
    const inputs = n.data?.inputs
    if (!inputs) return n
    const nextInputs: Record<string, FieldBinding> = {}
    let changed = false
    for (const [fieldName, binding] of Object.entries(inputs)) {
      if (
        binding &&
        (binding.expressionType === 'variable' || binding.expressionType === 'jsonpath' || binding.expressionType === 'interpolation')
      ) {
        const value = rewriteRefValue(binding.value, idToKey)
        if (value !== binding.value) changed = true
        nextInputs[fieldName] = { ...binding, value }
      } else {
        nextInputs[fieldName] = binding
      }
    }
    return changed ? { ...n, data: { ...n.data, inputs: nextInputs } } : n
  })
  return { ...editor, nodes }
}

/**
 * 编辑器画布 JSON → 引擎定义（保存/调试执行时调用）.
 * 以画布为唯一数据源：节点表单数据 + collectEdges 汇总的全量连线.
 * 节点 key 取 data.key（自定义）否则 id；引用值与连线/坐标统一按 id→key 换算.
 */
export function fromEditorFormat(editor: EditorWorkflowJSON, name: string, description?: string): WorkflowDefinition {
  const idToKey = new Map<string, string>()
  for (const n of editor.nodes ?? []) {
    if (!isSupportedNodeType(String(n.type))) continue
    idToKey.set(n.id, nodeKeyOf(n))
  }

  const nodes: WorkflowNodeDef[] = (editor.nodes ?? [])
    .filter((n) => isSupportedNodeType(String(n.type)))
    .map((n) => {
      const type = String(n.type) as NodeType
      const sanitized = type === 'switch' ? {} : sanitizeInputs(n.data?.inputs)

      // 变量引用里可能还写着旧 id（改过 key 但未重新加载），统一重写为新 key
      const inputs: Record<string, FieldBinding> = {}
      for (const [fieldName, binding] of Object.entries(sanitized)) {
        inputs[fieldName] =
          binding.expressionType === 'variable' || binding.expressionType === 'jsonpath' || binding.expressionType === 'interpolation'
            ? { ...binding, value: rewriteRefValue(binding.value, idToKey) }
            : binding
      }

      // 开始节点：data.inputs 是启动参数声明——映射为引擎 outputs（必需参数校验用），输出自动等于输入
      const outputs =
        type === 'start'
          ? sanitizeOutputs(
              Object.entries(inputs).map(([fieldName, binding]) => ({
                name: fieldName,
                fieldType: binding.fieldType ?? 'string',
                isRequired: binding.required !== false,
                ...(binding.description ? { description: binding.description } : {}),
              })),
            )
          : sanitizeOutputs(n.data?.outputs)

      // switch：data.branches → config.branches（顺序分支定义）；
      // condition：config.trueTarget（满足时走哪条出边的目标节点 key）按 id→key 重写
      const config =
        type === 'switch'
          ? {
              branches: (n.data?.branches ?? []).map((b) => ({
                id: b.id,
                label: b.label,
                binding: sanitizeBinding(b.binding),
              })),
            }
          : sanitizeSettings(n.data?.settings)

      if (type === 'condition' && typeof config.trueTarget === 'string') {
        const mapped = idToKey.get(config.trueTarget)
        if (mapped) {
          config.trueTarget = mapped
        }
      }

      return {
        key: idToKey.get(n.id) ?? n.id,
        name: String(n.data?.title ?? n.id),
        type,
        description: n.data?.content || undefined,
        config,
        inputs: type === 'start' || type === 'switch' ? {} : inputs,
        outputs,
      }
    })

  const nodeIds = new Set(nodes.map((n) => n.key))
  const typeOf = (id: string) => String(editor.nodes?.find((n) => n.id === id)?.type ?? '')

  const connections: WorkflowConnectionDef[] = []
  const seen = new Set<string>()
  for (const edge of collectEdges(editor)) {
    const sourceKey = idToKey.get(edge.sourceNodeID)
    const targetKey = idToKey.get(edge.targetNodeID)
    if (!sourceKey || !targetKey || !nodeIds.has(sourceKey) || !nodeIds.has(targetKey)) continue
    const id = `c_${sourceKey}_${targetKey}${edge.sourcePortID ? `_${edge.sourcePortID}` : ''}`
    if (seen.has(id)) continue
    seen.add(id)
    connections.push({
      id,
      source: sourceKey,
      target: targetKey,
      ...(typeOf(edge.sourceNodeID) === 'condition'
        ? (() => {
            // 条件节点：优先按 trueTarget 配置绑定「满足时走哪条出边」（出边目标节点 key），否则按端口 true/false
            const srcNode = editor.nodes?.find((n) => n.id === edge.sourceNodeID)
            const trueTarget = String((srcNode?.data?.settings as Record<string, unknown> | undefined)?.trueTarget ?? '')
            const trueTargetKey = idToKey.get(trueTarget) ?? trueTarget
            if (trueTargetKey) {
              return { condition: edge.targetNodeID === trueTargetKey ? 'true' : 'false' }
            }

            return CONDITION_VALUES.includes(String(edge.sourcePortID))
              ? { condition: String(edge.sourcePortID), label: String(edge.sourcePortID) === 'true' ? '满足' : '不满足' }
              : {}
          })()
        : typeOf(edge.sourceNodeID) === 'switch' && String(edge.sourcePortID ?? '') !== ''
          ? { condition: String(edge.sourcePortID) }
          : {}),
    })
  }

  const nodePositions: Record<string, { x: number; y: number }> = {}
  for (const node of editor.nodes ?? []) {
    if (node.meta?.position) {
      nodePositions[idToKey.get(node.id) ?? node.id] = { x: node.meta.position.x, y: node.meta.position.y }
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
  for (const key of ['aiModelId', 'pluginKey', 'code', 'conditionScript', 'trueTarget'] as const) {
    const value = settings[key]
    if (typeof value === 'string' && value !== '') {
      result[key] = value
    }
  }
  return result
}

/** 清洗单个绑定：剔除无效表达式类型 */
function sanitizeBinding(binding: FieldBinding | undefined): FieldBinding {
  const expressionType = String(binding?.expressionType)
  if (!['run', 'fixed', 'variable', 'jsonpath', 'interpolation'].includes(expressionType)) {
    return { expressionType: 'variable', value: '', required: true }
  }

  return {
    expressionType: expressionType as FieldBinding['expressionType'],
    value: String(binding?.value ?? ''),
    required: binding?.required !== false,
    ...(binding?.fieldType ? { fieldType: binding.fieldType } : {}),
    ...(binding?.description ? { description: binding.description } : {}),
  }
}

/** 清洗输入绑定：剔除无效字段与空引用；fieldType 透传（引擎仅作元数据存储） */
function sanitizeInputs(inputs: Record<string, FieldBinding> | undefined): Record<string, FieldBinding> {
  const result: Record<string, FieldBinding> = {}
  if (!inputs) return result
  for (const [fieldName, binding] of Object.entries(inputs)) {
    if (!binding || typeof binding !== 'object') continue
    result[fieldName] = sanitizeBinding(binding)
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

  // 节点 Key：data.key 自定义覆盖（画布 id 是内部标识，key 是引用前缀）。
  // 校验格式/保留字/唯一性，并建 key→id 映射供引用检查回溯节点.
  const keyToId = new Map<string, string>()
  const KEY_RE = /^[A-Za-z_][A-Za-z0-9_]*$/
  const RESERVED_KEYS = new Set(['sys', 'system', 'input'])
  for (const n of nodes) {
    const custom = String(n.data?.key ?? '').trim()
    const title = String(n.data?.title ?? n.id)
    if (custom) {
      if (!KEY_RE.test(custom)) {
        errors.push({ nodeId: n.id, message: `节点「${title}」Key 只能是字母/数字/下划线且以字母或下划线开头：${custom}` })
      } else if (RESERVED_KEYS.has(custom)) {
        errors.push({ nodeId: n.id, message: `节点「${title}」Key 不能使用保留字：${custom}` })
      }
    }
    const eff = custom || n.id
    if (keyToId.has(eff)) {
      errors.push({ nodeId: n.id, message: `存在重复的节点 Key：${eff}` })
    }
    keyToId.set(eff, n.id)
  }

  const startNodes = nodes.filter((n) => n.type === 'start')
  const endNodes = nodes.filter((n) => n.type === 'end')
  if (startNodes.length !== 1) errors.push({ message: '流程必须恰好包含一个开始节点' })
  if (endNodes.length === 0) errors.push({ message: '流程必须包含至少一个结束节点' })

  // 出边表：连线可能序列化在顶层 edges（画上画的）或节点内 edges（加载恢复的），统一汇总
  const outgoing = new Map<string, EditorEdgeJSON[]>()
  const incoming = new Map<string, number>()
  for (const edge of collectEdges(editor)) {
    if (!idSet.has(edge.sourceNodeID) || !idSet.has(edge.targetNodeID)) continue
    if (edge.sourceNodeID === edge.targetNodeID) continue
    const list = outgoing.get(edge.sourceNodeID) ?? []
    list.push(edge)
    outgoing.set(edge.sourceNodeID, list)
    incoming.set(edge.targetNodeID, (incoming.get(edge.targetNodeID) ?? 0) + 1)
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

    // 多条件节点：出边分支标记必须对应已配置分支（或 else），不允许重复
    if (node.type === 'switch') {
      const branchDefs = node.data?.branches ?? []
      const branchIds = branchDefs.map((b) => b.id).filter(Boolean)
      const title = String(node.data?.title ?? node.id)
      if (branchIds.length === 0) {
        errors.push({ nodeId: node.id, message: `多条件节点「${title}」至少需要配置一个分支` })
      }

      const branchSet = new Set(branchIds)
      const seenBranch = new Set<string>()
      for (const edge of outs) {
        const marker = String(edge.sourcePortID ?? '')
        if (!marker) {
          errors.push({ nodeId: node.id, message: `多条件节点「${title}」存在未绑定分支出边` })
          continue
        }

        if (marker !== 'else' && !branchSet.has(marker)) {
          errors.push({ nodeId: node.id, message: `多条件节点「${title}」的出边分支标记无效：${marker}` })
        }

        if (seenBranch.has(marker)) {
          errors.push({ nodeId: node.id, message: `多条件节点「${title}」存在重复的分支出边：${marker}` })
        }

        seenBranch.add(marker)
      }
    }
  }

  // 环检测（DFS 三色）
  if (startNodes.length === 1 && hasCycle(nodes, outgoing)) {
    errors.push({ message: '流程存在环路，不允许循环连接' })
  }

  // 变量引用必须是上游节点（前缀可以是节点 id，也可以是其自定义 Key）
  for (const node of nodes) {
    const ancestors = collectAncestors(node.id, outgoing)
    for (const [fieldName, binding] of Object.entries(node.data?.inputs ?? {})) {
      if (binding?.expressionType !== 'variable' || !binding.value) continue
      const prefix = binding.value.split('.')[0]
      if (!prefix || prefix === 'sys' || prefix === 'system' || prefix === 'input') continue
      const refId = keyToId.get(prefix) ?? (idSet.has(prefix) ? prefix : '')
      if (!refId) {
        errors.push({ nodeId: node.id, message: `输入 ${fieldName} 引用了不存在的节点：${binding.value}` })
      } else if (!ancestors.has(refId)) {
        errors.push({ nodeId: node.id, message: `输入 ${fieldName} 引用了非上游节点：${binding.value}` })
      }
    }
  }

  // 多条件节点分支绑定的变量引用也必须是上游节点
  for (const node of nodes) {
    if (node.type !== 'switch') continue
    const ancestors = collectAncestors(node.id, outgoing)
    const branchDefs = node.data?.branches ?? []
    for (let bi = 0; bi < branchDefs.length; bi++) {
      const binding = branchDefs[bi]?.binding
      if (!binding || binding.expressionType !== 'variable' || !binding.value) continue
      const prefix = binding.value.split('.')[0]
      if (!prefix || prefix === 'sys' || prefix === 'system' || prefix === 'input') continue
      const refId = keyToId.get(prefix) ?? (idSet.has(prefix) ? prefix : '')
      const label = branchDefs[bi].label || `分支 ${bi + 1}`
      if (!refId) {
        errors.push({ nodeId: node.id, message: `分支「${label}」引用了不存在的节点：${binding.value}` })
      } else if (!ancestors.has(refId)) {
        errors.push({ nodeId: node.id, message: `分支「${label}」引用了非上游节点：${binding.value}` })
      }
    }
  }

  return errors
}

/** 上游变量选项（供输入绑定的智能提示）：系统变量 + 全部祖先节点的输出 */
export interface VariableOption {
  value: string
  label: string
}

export function collectUpstreamVariables(
  editor: EditorWorkflowJSON | null,
  nodeId: string | null,
  globalVariables?: { name: string; description?: string }[],
): VariableOption[] {
  const options: VariableOption[] = []
  for (const variable of globalVariables ?? []) {
    if (!variable.name) continue
    options.push({
      value: `system.${variable.name}`,
      label: `system.${variable.name}${variable.description ? `（${variable.description}）` : ''}`,
    })
  }
  if (!editor || !nodeId) return options
  const nodes = editor.nodes ?? []
  const byId = new Map(nodes.map((n) => [n.id, n]))

  const outgoing = new Map<string, EditorEdgeJSON[]>()
  const incoming = new Map<string, string[]>()
  for (const edge of collectEdges(editor)) {
    const list = outgoing.get(edge.sourceNodeID) ?? []
    list.push(edge)
    outgoing.set(edge.sourceNodeID, list)
    const inList = incoming.get(edge.targetNodeID) ?? []
    inList.push(edge.sourceNodeID)
    incoming.set(edge.targetNodeID, inList)
  }

  // 祖先集合（含被跳过分支的节点——运行时 required=false 才可引用，这里仅作提示）
  const ancestors = new Set<string>()
  const queue = [...(incoming.get(nodeId) ?? [])]
  while (queue.length > 0) {
    const current = queue.shift() as string
    if (ancestors.has(current)) continue
    ancestors.add(current)
    queue.push(...(incoming.get(current) ?? []))
  }

  options.push(
    { value: 'sys.instanceId', label: 'sys.instanceId（实例 ID）' },
    { value: 'sys.workflowId', label: 'sys.workflowId（流程 ID）' },
    { value: 'sys.startedAt', label: 'sys.startedAt（开始时间）' },
  )
  for (const id of ancestors) {
    const node = byId.get(id)
    if (!node) continue
    const title = String(node.data?.title ?? id)
    const key = nodeKeyOf(node)
    // 开始节点透传：声明的输入参数即输出变量
    if (node.type === 'start') {
      for (const [name, binding] of Object.entries(node.data?.inputs ?? {})) {
        if (!name) continue
        options.push({ value: `${key}.${name}`, label: `${key}.${name}${binding.description ? `（${binding.description}）` : ''}` })
      }
      continue
    }
    for (const output of node.data?.outputs ?? []) {
      if (!output.name) continue
      options.push({ value: `${key}.${output.name}`, label: `${title}.${output.name}` })
    }
  }
  return options
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
