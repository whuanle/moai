/**
 * 编辑器 JSON（FlowGram 画布）与引擎 WorkflowDefinition（nodes + connections DAG）的双向转换，
 * 以及与后端验证器同构的客户端快速校验.
 */

import { generateNodeId, getNodeTemplate, isSupportedNodeType, NODE_CONSTRAINTS } from './constants'
import type {
  ClassifierClassDef,
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

/** 问题分类节点聊天记录条数上限（与引擎 QuestionClassifierNodeExecutor.MaxHistoryCount 一致） */
export const CLASSIFIER_HISTORY_MAX = 50

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

/**
 * 核心节点保护（加载草稿时调用）：开始/结束节点必须存在、开始节点唯一.
 * 空画布（历史草稿可能落库为 {}）回退默认编排；缺失的开始/结束节点按现有节点范围补齐；
 * 重复的开始节点仅保留首个（开始节点不可删除，多出的无法在画布上移除）.
 */
export function ensureCoreNodes(editor: EditorWorkflowJSON): EditorWorkflowJSON {
  const nodes = (editor.nodes ?? []).filter((n) => isSupportedNodeType(String(n.type)))
  if (nodes.length === 0) return createDefaultEditorData()

  const firstStartIndex = nodes.findIndex((n) => n.type === 'start')
  const hasEnd = nodes.some((n) => n.type === 'end')
  if (firstStartIndex >= 0 && hasEnd && nodes.filter((n) => n.type === 'start').length === 1) {
    return editor
  }

  const repaired = nodes.filter((n, i) => n.type !== 'start' || i === firstStartIndex)

  // 已有节点包围盒：补齐的核心节点摆在范围外侧，加载后 fitView 保证可见
  const xs = repaired.map((n) => n.meta?.position?.x ?? 0)
  const ys = repaired.map((n) => n.meta?.position?.y ?? 0)
  const minX = Math.min(...xs)
  const maxX = Math.max(...xs)
  const minY = Math.min(...ys)

  // 固定 id 已被占用时加后缀，避免与画布现有节点冲突
  const startId = repaired.some((n) => n.id === 'start') ? 'start_repaired' : generateNodeId('start')
  const endId = repaired.some((n) => n.id === 'end') ? 'end_repaired' : generateNodeId('end')

  const result = [...repaired]
  if (firstStartIndex < 0) {
    result.unshift({
      id: startId,
      type: 'start',
      meta: { position: { x: minX - 360, y: minY }, defaultExpanded: true },
      data: nodeDataFromTemplate('start'),
      blocks: [],
      edges: [],
    })
  }
  if (!hasEnd) {
    result.push({
      id: endId,
      type: 'end',
      meta: { position: { x: maxX + 360, y: minY }, defaultExpanded: true },
      data: nodeDataFromTemplate('end'),
      blocks: [],
      edges: [],
    })
  }
  return { ...editor, nodes: result }
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

  if (type === 'questionClassifier') {
    return {
      title: template?.name ?? type,
      content: template?.desc ?? '',
      inputs: template ? JSON.parse(JSON.stringify(template.inputs)) : {},
      outputs: template ? JSON.parse(JSON.stringify(template.outputs)) : [],
      settings: template?.settings ? { ...template.settings } : {},
      classes: [
        { id: 'c1', label: '' },
        { id: 'c2', label: '' },
      ],
    }
  }

  return {
    title: template?.name ?? type,
    content: template?.desc ?? '',
    inputs: template ? JSON.parse(JSON.stringify(template.inputs)) : {},
    outputs: template ? JSON.parse(JSON.stringify(template.outputs)) : [],
    // 深拷贝：模板 settings 含嵌套结构（http 的 params/headers/auth/extract），避免多节点共享引用
    settings: template?.settings ? (JSON.parse(JSON.stringify(template.settings)) as NodeSettings) : {},
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
      ...((sourceNode?.type === 'condition' || sourceNode?.type === 'switch' || sourceNode?.type === 'questionClassifier') && conn.condition
        ? { sourcePortID: conn.condition }
        : {}),
    })
  }

  const nodes: EditorNodeJSON[] = definition.nodes
    .filter((n) => isSupportedNodeType(n.type))
    .map((n) => {
      // 开始节点：固定唯一启动参数 question（用户问题）；旧定义的自定义参数收敛为 question
      const startInputs =
        n.type === 'start'
          ? { question: { expressionType: 'run', value: '', required: true, fieldType: 'string' } as FieldBinding }
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

      // questionClassifier：config.classes 还原为 data.classes 供分类编辑器展示，设置项从 config 摘出
      const classes =
        n.type === 'questionClassifier' && Array.isArray(configObj.classes)
          ? (configObj.classes as Record<string, unknown>[]).map((c, i) => ({
              id: String(c.id ?? `c${i + 1}`),
              label: String(c.label ?? ''),
            }))
          : undefined
      const classifierSettings =
        n.type === 'questionClassifier'
          ? {
              aiModelId: String(configObj.aiModelId ?? ''),
              backgroundKnowledge: String(configObj.backgroundKnowledge ?? ''),
              historyCount: Number.isFinite(Number(configObj.historyCount)) ? Number(configObj.historyCount) : 6,
            }
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
          classes,
          inputs: startInputs ?? n.inputs ?? {},
          outputs: n.type === 'start' ? [] : (n.outputs ?? []),
          settings: n.type === 'switch' ? {} : (classifierSettings ?? (n.config ?? {})),
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

/** 旧参数引用改写：`<startKey>.query` → `<startKey>.question`，要求后面是引用边界（结尾/`.`/`}`/非标识符），避免误伤 queryX 等同前缀字段 */
function rewriteLegacyQueryRef(value: string, startKey: string): string {
  const token = `${startKey}.query`
  let out = ''
  let from = 0
  for (let idx = value.indexOf(token, from); idx >= 0; idx = value.indexOf(token, from)) {
    const after = idx + token.length
    const next = value[after]
    const isBoundary = next === undefined || !/[A-Za-z0-9_]/.test(next)
    out += value.slice(from, idx) + (isBoundary ? `${startKey}.question` : token)
    from = after
  }
  return out + value.slice(from)
}

/** 递归改写节点数据中所有字符串叶子（绑定值、分支条件、HTTP 配置、JS 脚本内的引用一并迁移） */
function rewriteDataQueryRefs<T>(data: T, startKey: string): T {
  if (typeof data === 'string') return rewriteLegacyQueryRef(data, startKey) as unknown as T
  if (Array.isArray(data)) return data.map((item) => rewriteDataQueryRefs(item, startKey)) as unknown as T
  if (data && typeof data === 'object') {
    const out: Record<string, unknown> = {}
    for (const [key, value] of Object.entries(data)) {
      out[key] = rewriteDataQueryRefs(value, startKey)
    }
    return out as unknown as T
  }
  return data
}

/**
 * 旧草稿画布收敛到固定 question 契约（加载 draftEditorData 时调用，与 toEditorFormat 的收敛规则一致）：
 * start 节点只保留唯一启动参数 question；下游对旧参数 `<startKey>.query` 的引用（变量/插值/脚本）
 * 改写为 `<startKey>.question`。其余自定义旧参数无等价物，引用留待校验/运行期报错由用户手工修复.
 */
export function convergeStartContract(editor: EditorWorkflowJSON): EditorWorkflowJSON {
  const startNode = (editor.nodes ?? []).find((n) => n.type === 'start')
  if (!startNode) return editor
  const startKey = nodeKeyOf(startNode)

  const nodes = (editor.nodes ?? []).map((n) => {
    if (n.type === 'start') {
      const data = {
        ...n.data,
        inputs: { question: { expressionType: 'run', value: '', required: true, fieldType: 'string' } as FieldBinding },
      }
      return { ...n, data }
    }
    return n.data ? { ...n, data: rewriteDataQueryRefs(n.data, startKey) } : n
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

      // 开始节点：固定唯一启动参数 question（用户问题，流程对话时由服务端注入），不再支持自定义输入/输出字段
      const outputs =
        type === 'start'
          ? [{ name: 'question', fieldType: 'string', isRequired: true }]
          : sanitizeOutputs(n.data?.outputs)

      // switch：data.branches → config.branches（顺序分支定义）；
      // questionClassifier：data.classes → config.classes（分类定义，出边标记 = 分类 id）；
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
          : type === 'questionClassifier'
            ? {
                ...sanitizeSettings(n.data?.settings),
                classes: sanitizeClasses(n.data?.classes),
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
        : (typeOf(edge.sourceNodeID) === 'switch' || typeOf(edge.sourceNodeID) === 'questionClassifier') && String(edge.sourcePortID ?? '') !== ''
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
  for (const key of ['aiModelId', 'systemPrompt', 'agentAppId', 'pluginKey', 'code', 'conditionScript', 'trueTarget', 'backgroundKnowledge'] as const) {
    const value = settings[key]
    if (typeof value === 'string' && value !== '') {
      result[key] = value
    }
  }

  // aiChat：采样温度 0-2（越界丢弃，用渠道默认）
  const temperature = Number(settings.temperature)
  if (Number.isFinite(temperature) && temperature >= 0 && temperature <= 2) {
    result.temperature = Math.round(temperature * 100) / 100
  }

  // knowledgeSearch：静态知识库（单个）+ topK（每库召回条数，1-50）；wikiIds 为旧草稿兼容
  const wikiId = Number(settings.wikiId)
  if (Number.isInteger(wikiId) && wikiId > 0) {
    result.wikiId = wikiId
  }

  if (Array.isArray(settings.wikiIds)) {
    const ids = [...new Set(settings.wikiIds.map(Number).filter((n) => Number.isInteger(n) && n > 0))]
    if (ids.length > 0) {
      result.wikiIds = ids
    }
  }

  const topK = Number(settings.topK)
  if (Number.isInteger(topK) && topK >= 1) {
    result.topK = Math.min(topK, 50)
  }

  // questionClassifier：聊天记录条数（0-50，缺省由引擎取默认值 6）
  const historyCount = Number(settings.historyCount)
  if (Number.isInteger(historyCount) && historyCount >= 0) {
    result.historyCount = Math.min(historyCount, CLASSIFIER_HISTORY_MAX)
  }

  // http：请求方法/地址/超时/Body 类型/报错捕获
  const httpMethod = String(settings.method ?? '').toUpperCase()
  if (['GET', 'POST', 'PUT', 'DELETE', 'PATCH', 'HEAD'].includes(httpMethod)) {
    result.method = httpMethod
  }

  const url = String(settings.url ?? '')
  if (url.trim() !== '') {
    result.url = url
  }

  const timeoutSeconds = Number(settings.timeoutSeconds)
  if (Number.isInteger(timeoutSeconds) && timeoutSeconds >= 1) {
    result.timeoutSeconds = Math.min(timeoutSeconds, 300)
  }

  if (['none', 'json', 'form', 'text'].includes(String(settings.bodyType))) {
    result.bodyType = settings.bodyType
  }

  if (typeof settings.body === 'string' && settings.body !== '') {
    result.body = settings.body
  }

  if (settings.errorCapture === true) {
    result.errorCapture = true
  }

  // http：键值对列表（查询参数/请求头/表单字段），剔除无名项
  const sanitizeKvList = (value: unknown): { name: string; value: string }[] | undefined => {
    if (!Array.isArray(value)) return undefined
    const list = value
      .filter((item): item is { name?: unknown; value?: unknown } => !!item && typeof item === 'object')
      .map((item) => ({ name: String(item.name ?? ''), value: String(item.value ?? '') }))
      .filter((item) => item.name.trim() !== '')
    return list.length > 0 ? list : undefined
  }

  const params = sanitizeKvList(settings.params)
  if (params) result.params = params
  const headers = sanitizeKvList(settings.headers)
  if (headers) result.headers = headers
  const formEntries = sanitizeKvList(settings.formEntries)
  if (formEntries) result.formEntries = formEntries

  // http：鉴权配置（按类型保留所需字段）
  if (settings.auth && typeof settings.auth === 'object') {
    const rawAuth = settings.auth as Record<string, unknown>
    const authType = String(rawAuth.type ?? 'none')
    if (['none', 'bearer', 'basic', 'apiKey'].includes(authType)) {
      const str = (v: unknown) => (typeof v === 'string' && v !== '' ? v : undefined)
      result.auth = {
        type: authType as 'none' | 'bearer' | 'basic' | 'apiKey',
        ...(authType === 'bearer' ? { token: str(rawAuth.token) } : {}),
        ...(authType === 'basic' ? { username: str(rawAuth.username), password: str(rawAuth.password) } : {}),
        ...(authType === 'apiKey' ? { headerName: str(rawAuth.headerName), headerValue: str(rawAuth.headerValue) } : {}),
      }
    }
  }

  // http：输出字段提取（名称/JsonPath 非空且名称唯一）
  if (Array.isArray(settings.extract)) {
    const seen = new Set<string>()
    const extract = settings.extract
      .filter((item): item is { name?: unknown; path?: unknown; fieldType?: unknown } => !!item && typeof item === 'object')
      .map((item) => ({
        name: String(item.name ?? '').trim(),
        path: String(item.path ?? '').trim(),
        fieldType: ['string', 'number', 'boolean', 'object', 'map', 'array', 'dynamic'].includes(String(item.fieldType))
          ? String(item.fieldType)
          : 'string',
      }))
      .filter((item) => {
        if (item.name === '' || item.path === '' || seen.has(item.name)) return false
        seen.add(item.name)
        return true
      })
    if (extract.length > 0) {
      result.extract = extract
    }
  }

  return result
}

/** 清洗问题分类节点分类列表：保留 id 非空且唯一的项（label 允许为空，校验层提示） */
function sanitizeClasses(classes: ClassifierClassDef[] | undefined): { id: string; label: string }[] {
  if (!Array.isArray(classes)) return []
  const result: { id: string; label: string }[] = []
  for (const item of classes) {
    const id = String(item?.id ?? '').trim()
    if (!id || result.some((c) => c.id === id)) continue
    result.push({ id, label: String(item?.label ?? '') })
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

/**
 * 团队插件响应 schema → 节点输出参数声明（name/fieldType/description）.
 * schema 为空时返回空数组，由调用方决定是否保留原输出.
 */
export function outputsFromPluginSchema(
  schema:
    | { name?: string | null; fieldType?: string | null; description?: string | null }[]
    | null
    | undefined,
): OutputField[] {
  if (!Array.isArray(schema)) return []
  return schema
    .filter((f) => f && typeof f.name === 'string' && f.name !== '')
    .map((f) => ({
      name: f.name as string,
      fieldType: String(f.fieldType ?? 'dynamic'),
      description: f.description ?? '',
    }))
}

/**
 * 团队插件请求参数 schema → 节点输入参数绑定（fixed 空值待填/待绑定，描述来自参数注释）.
 * schema 为空时返回空对象，由调用方决定是否保留原输入.
 */
export function inputsFromPluginSchema(
  schema:
    | { name?: string | null; fieldType?: string | null; description?: string | null }[]
    | null
    | undefined,
): Record<string, FieldBinding> {
  const result: Record<string, FieldBinding> = {}
  if (!Array.isArray(schema)) return result
  for (const f of schema) {
    if (!f || typeof f.name !== 'string' || f.name === '') continue
    result[f.name] = {
      expressionType: 'fixed',
      value: '',
      required: true,
      fieldType: String(f.fieldType ?? 'string'),
      ...(f.description ? { description: f.description } : {}),
    }
  }

  return result
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

    // 普通节点（非条件/多条件/问题分类/结束）只允许一条输出连线：拖线即切换下游，分支由条件节点承担
    if (node.type !== 'condition' && node.type !== 'switch' && node.type !== 'questionClassifier' && node.type !== 'end' && outs.length > 1) {
      errors.push({ nodeId: node.id, message: `节点「${node.data?.title ?? node.id}」只允许一条输出连线（分支请使用条件/多条件节点）` })
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
    // 问题分类节点：分类名非空/不重复；出边分类标记必须对应已配置分类（无 else），不允许重复
    if (node.type === 'questionClassifier') {
      const classDefs = node.data?.classes ?? []
      const classIds = classDefs.map((c) => String(c.id ?? '').trim()).filter(Boolean)
      const title = String(node.data?.title ?? node.id)
      if (classIds.length === 0) {
        errors.push({ nodeId: node.id, message: `问题分类节点「${title}」至少需要配置一个分类` })
      }

      if (classDefs.some((c) => !String(c.label ?? '').trim())) {
        errors.push({ nodeId: node.id, message: `问题分类节点「${title}」的分类值不可为空` })
      }

      const labels = classDefs.map((c) => String(c.label ?? '').trim()).filter(Boolean)
      const duplicatedLabel = labels.find((label, i) => labels.indexOf(label) !== i)
      if (duplicatedLabel) {
        errors.push({ nodeId: node.id, message: `问题分类节点「${title}」存在重复的分类名称：${duplicatedLabel}` })
      }

      const classSet = new Set(classIds)
      const seenClass = new Set<string>()
      for (const edge of outs) {
        const marker = String(edge.sourcePortID ?? '')
        if (!marker) {
          errors.push({ nodeId: node.id, message: `问题分类节点「${title}」存在未绑定分类的出边` })
          continue
        }

        if (!classSet.has(marker)) {
          errors.push({ nodeId: node.id, message: `问题分类节点「${title}」的出边分类标记无效：${marker}` })
        }

        if (seenClass.has(marker)) {
          errors.push({ nodeId: node.id, message: `问题分类节点「${title}」存在重复的分类出边：${marker}` })
        }

        seenClass.add(marker)
      }
    }
    // aiChat 节点：模型必选（保存期拦截，与 HTTP 节点请求地址同级校验）
    if (node.type === 'aiChat') {
      const title = String(node.data?.title ?? node.id)
      if (!String(node.data?.settings?.aiModelId ?? '').trim()) {
        errors.push({ nodeId: node.id, message: `AI 对话节点「${title}」未配置模型，请在节点配置中选择 AI 模型` })
      }
    }

    // agentApp 节点：必须选择 Agent 应用
    if (node.type === 'agentApp') {
      const title = String(node.data?.title ?? node.id)
      const agentAppId = String(node.data?.settings?.agentAppId ?? '').trim()
      if (!agentAppId) {
        errors.push({ nodeId: node.id, message: `Agent 应用节点「${title}」未选择应用，请在节点配置中选择 Agent 应用` })
      }
    }

    // http 节点：请求地址必填；提取字段名唯一且 JsonPath 非空；配置中的 {引用} 插值必须是上游节点
    if (node.type === 'http') {
      const title = String(node.data?.title ?? node.id)
      const httpSettings = (node.data?.settings ?? {}) as Record<string, unknown>
      if (String(httpSettings.url ?? '').trim() === '') {
        errors.push({ nodeId: node.id, message: `HTTP 请求节点「${title}」未配置请求地址` })
      }

      const httpAncestors = collectAncestors(node.id, outgoing)
      const seenExtract = new Set<string>()
      const extractList = Array.isArray(httpSettings.extract) ? (httpSettings.extract as { name?: unknown; path?: unknown }[]) : []
      for (const field of extractList) {
        const name = String(field?.name ?? '').trim()
        const path = String(field?.path ?? '').trim()
        if (!name) {
          errors.push({ nodeId: node.id, message: `HTTP 请求节点「${title}」存在名称为空的提取字段` })
          continue
        }

        if (seenExtract.has(name)) {
          errors.push({ nodeId: node.id, message: `HTTP 请求节点「${title}」存在重复的提取字段名：${name}` })
        }

        seenExtract.add(name)
        if (!path) {
          errors.push({ nodeId: node.id, message: `HTTP 请求节点「${title}」的提取字段 ${name} 缺少 JsonPath 表达式` })
        }
      }

      const templates: string[] = [String(httpSettings.url ?? ''), String(httpSettings.body ?? '')]
      for (const list of [httpSettings.params, httpSettings.headers, httpSettings.formEntries]) {
        if (!Array.isArray(list)) continue
        for (const item of list as { value?: unknown }[]) {
          templates.push(String(item?.value ?? ''))
        }
      }

      for (const template of templates) {
        for (const match of template.matchAll(/\{([^{}]+)\}/g)) {
          const reference = match[1]!.trim()
          const prefix = reference.split('.')[0] ?? ''
          if (!prefix || prefix === 'sys' || prefix === 'system' || prefix === 'input') continue
          const refId = keyToId.get(prefix) ?? (idSet.has(prefix) ? prefix : '')
          if (!refId) {
            errors.push({ nodeId: node.id, message: `HTTP 请求节点「${title}」引用了不存在的节点：{${reference}}` })
          } else if (!httpAncestors.has(refId)) {
            errors.push({ nodeId: node.id, message: `HTTP 请求节点「${title}」引用了非上游节点：{${reference}}` })
          }
        }
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
    const title = String(node.data?.title ?? node.id)
    for (const [fieldName, binding] of Object.entries(node.data?.inputs ?? {})) {
      if (binding?.expressionType !== 'variable' || !binding.value) continue
      const prefix = binding.value.split('.')[0]
      if (!prefix || prefix === 'sys' || prefix === 'system' || prefix === 'input') continue
      const refId = keyToId.get(prefix) ?? (idSet.has(prefix) ? prefix : '')
      if (!refId) {
        errors.push({ nodeId: node.id, message: `节点「${title}」的输入 ${fieldName} 引用了不存在的节点：${binding.value}` })
      } else if (!ancestors.has(refId)) {
        errors.push({ nodeId: node.id, message: `节点「${title}」的输入 ${fieldName} 引用了非上游节点：${binding.value}` })
      }
    }
  }

  // 多条件节点分支绑定的变量引用也必须是上游节点
  for (const node of nodes) {
    if (node.type !== 'switch') continue
    const ancestors = collectAncestors(node.id, outgoing)
    const title = String(node.data?.title ?? node.id)
    const branchDefs = node.data?.branches ?? []
    for (let bi = 0; bi < branchDefs.length; bi++) {
      const binding = branchDefs[bi]?.binding
      if (!binding || binding.expressionType !== 'variable' || !binding.value) continue
      const prefix = binding.value.split('.')[0]
      if (!prefix || prefix === 'sys' || prefix === 'system' || prefix === 'input') continue
      const refId = keyToId.get(prefix) ?? (idSet.has(prefix) ? prefix : '')
      const label = branchDefs[bi].label || `分支 ${bi + 1}`
      if (!refId) {
        errors.push({ nodeId: node.id, message: `节点「${title}」的分支「${label}」引用了不存在的节点：${binding.value}` })
      } else if (!ancestors.has(refId)) {
        errors.push({ nodeId: node.id, message: `节点「${title}」的分支「${label}」引用了非上游节点：${binding.value}` })
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

/**
 * 对话系统变量：发布应用对话时由服务端自动注入 sys.*，
 * history 为 [{role, content}] 数组，可直接作为 AI 对话节点的 history 输入.
 */
export const CONVERSATION_SYS_VARIABLES: VariableOption[] = [
  { value: 'sys.userId', label: 'sys.userId（使用者 ID）' },
  { value: 'sys.appId', label: 'sys.appId（应用 ID）' },
  { value: 'sys.conversationId', label: 'sys.conversationId（当前对话 ID）' },
  { value: 'sys.messageId', label: 'sys.messageId（AI 回复的 ID）' },
  { value: 'sys.history', label: 'sys.history（历史记录）' },
]

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
    { value: 'sys.currentTime', label: 'sys.currentTime（当前时间）' },
  )
  // 对话系统变量：发布应用对话时自动注入（sys.userId/sys.appId/sys.conversationId/sys.messageId/sys.history）
  options.push(...CONVERSATION_SYS_VARIABLES)
  for (const id of ancestors) {
    const node = byId.get(id)
    if (!node) continue
    const title = String(node.data?.title ?? id)
    const key = nodeKeyOf(node)
    // 开始节点：固定 question 输出（data.inputs 恒为 question 声明）
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
