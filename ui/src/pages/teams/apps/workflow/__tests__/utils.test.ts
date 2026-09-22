import { describe, expect, it } from 'vitest'
import {
  collectEdges,
  convergeStartContract,
  inputsFromPluginSchema,
  outputsFromPluginSchema,
  collectUpstreamVariables,
  normalizeEditorData,
  normalizeReferences,
  createDefaultEditorData,
  ensureCoreNodes,
  fromEditorFormat,
  nodeDataFromTemplate,
  toEditorFormat,
  validateEditorData,
} from '../utils'
import { parseCurlCommand } from '../curl'
import type { WorkflowDefinition } from '../types'

/** 与后端 DemoDefinition 同构的定义 */
function buildDefinition(): WorkflowDefinition {
  return {
    id: 'app-1',
    name: '文档问答',
    version: 1,
    status: 'draft',
    nodes: [
      {
        key: 'start',
        name: '开始',
        type: 'start',
        inputs: {},
        outputs: [{ name: 'question', fieldType: 'string', isRequired: true }],
      },
      {
        key: 'search',
        name: '检索',
        type: 'plugin',
        config: { pluginKey: 'mock.knowledgeSearch' },
        inputs: { query: { expressionType: 'variable', value: 'start.question', required: true } },
        outputs: [
          { name: 'documents', fieldType: 'array' },
          { name: 'hasResult', fieldType: 'boolean' },
        ],
      },
      {
        key: 'check',
        name: '条件',
        type: 'condition',
        inputs: { condition: { expressionType: 'variable', value: 'search.hasResult' } },
        outputs: [],
      },
      {
        key: 'answer',
        name: 'AI 回答',
        type: 'aiChat',
        config: { aiModelId: 'model-1' },
        inputs: {
          prompt: {
            expressionType: 'interpolation',
            value: '问题：{start.question}',
            required: true,
          },
        },
        outputs: [{ name: 'answer', fieldType: 'string' }],
      },
      {
        key: 'fallback',
        name: '兜底',
        type: 'plugin',
        config: { pluginKey: 'mock.fallback' },
        inputs: { query: { expressionType: 'variable', value: 'start.question' } },
        outputs: [{ name: 'answer', fieldType: 'string' }],
      },
      {
        key: 'end',
        name: '结束',
        type: 'end',
        inputs: {
          answer: { expressionType: 'variable', value: 'answer.answer', required: false },
          fallbackAnswer: { expressionType: 'variable', value: 'fallback.answer', required: false },
        },
        outputs: [],
      },
    ],
    connections: [
      { id: 'c1', source: 'start', target: 'search' },
      { id: 'c2', source: 'search', target: 'check' },
      { id: 'c3', source: 'check', target: 'answer', condition: 'true', label: '满足' },
      { id: 'c4', source: 'check', target: 'fallback', condition: 'false', label: '不满足' },
      { id: 'c5', source: 'answer', target: 'end' },
      { id: 'c6', source: 'fallback', target: 'end' },
    ],
    ui: { nodePositions: { start: { x: 80, y: 200 } } },
  }
}

describe('workflow converter', () => {
  it('空定义生成默认 start → end 画布', () => {
    const editor = toEditorFormat(null)
    expect(editor.nodes.map((n) => n.type)).toEqual(['start', 'end'])
    expect(editor.nodes[0].edges?.[0].targetNodeID).toBe(editor.nodes[1].id)
    expect(validateEditorData(editor)).toEqual([])
  })

  it('定义 → 编辑器：连线在顶层且条件节点出边带 true/false 端口', () => {
    const editor = toEditorFormat(buildDefinition())
    // 画布上画的连线由 toJSON 序列化在顶层 edges
    const checkOuts = collectEdges(editor).filter((e) => e.sourceNodeID === 'check')
    const ports = checkOuts.map((e) => String(e.sourcePortID)).sort()
    expect(ports).toEqual(['false', 'true'])
  })

  it('编辑器 → 定义：完整往返保留节点/连接/条件/配置', () => {
    const definition = buildDefinition()
    const editor = toEditorFormat(definition)
    const restored = fromEditorFormat(editor, '文档问答')

    expect(restored.nodes.map((n) => n.key)).toEqual(definition.nodes.map((n) => n.key))
    expect(restored.nodes.find((n) => n.key === 'search')?.config).toEqual({ pluginKey: 'mock.knowledgeSearch' })
    expect(restored.nodes.find((n) => n.key === 'answer')?.config).toEqual({ aiModelId: 'model-1' })

    // 条件连接带 condition 标记
    const checkOuts = restored.connections.filter((c) => c.source === 'check')
    expect(checkOuts.map((c) => c.condition).sort()).toEqual(['false', 'true'])

    // 输入绑定保留
    expect(restored.nodes.find((n) => n.key === 'end')?.inputs.fallbackAnswer.required).toBe(false)

    // 位置随 ui 返回
    expect(restored.ui?.nodePositions?.start).toEqual({ x: 80, y: 200 })

    // 二次转换稳定（往返幂等）
    const editor2 = toEditorFormat(restored)
    expect(validateEditorData(editor2)).toEqual([])
  })

  it('二次往返的条件连接不重复、不丢失', () => {
    const restored = fromEditorFormat(toEditorFormat(buildDefinition()), 'x')
    const restored2 = fromEditorFormat(toEditorFormat(restored), 'x')
    expect(restored2.connections.map((c) => [c.source, c.target, c.condition ?? ''].join('>')).sort()).toEqual(
      restored.connections.map((c) => [c.source, c.target, c.condition ?? ''].join('>')).sort(),
    )
  })

  it('nodeDataFromTemplate 提供模板默认值', () => {
    const data = nodeDataFromTemplate('javaScript')
    expect(data?.settings?.code).toContain('function run')
    expect(Object.keys(data?.inputs ?? {}).length).toBe(0)
    expect(data?.outputs?.[0].name).toBe('result')
  })

  it('默认画布通过校验', () => {
    expect(validateEditorData(createDefaultEditorData())).toEqual([])
  })

  it('ensureCoreNodes：空画布回退默认 start → end 编排', () => {
    const repaired = ensureCoreNodes({ nodes: [], edges: [] })
    expect(repaired.nodes.map((n) => n.type)).toEqual(['start', 'end'])
    expect(collectEdges(repaired).length).toBe(1)
    expect(validateEditorData(repaired)).toEqual([])
  })

  it('ensureCoreNodes：缺失的开始/结束节点按现有节点范围补齐', () => {
    const full = toEditorFormat(buildDefinition())

    const withoutStart = { ...full, nodes: full.nodes.filter((n) => n.type !== 'start') }
    const repairedStart = ensureCoreNodes(withoutStart)
    expect(repairedStart.nodes.filter((n) => n.type === 'start').length).toBe(1)
    const minX = Math.min(...withoutStart.nodes.map((n) => n.meta?.position?.x ?? 0))
    expect(repairedStart.nodes.find((n) => n.type === 'start')?.meta?.position?.x).toBeLessThan(minX)

    const withoutEnd = { ...full, nodes: full.nodes.filter((n) => n.type !== 'end') }
    const repairedEnd = ensureCoreNodes(withoutEnd)
    expect(repairedEnd.nodes.filter((n) => n.type === 'end').length).toBe(1)
    const maxX = Math.max(...withoutEnd.nodes.map((n) => n.meta?.position?.x ?? 0))
    expect(repairedEnd.nodes.find((n) => n.type === 'end')?.meta?.position?.x).toBeGreaterThan(maxX)
  })

  it('ensureCoreNodes：重复的开始节点仅保留首个（不可删除节点的去重自愈）', () => {
    const full = toEditorFormat(buildDefinition())
    const duplicated = { ...full, nodes: [...full.nodes, ...full.nodes.filter((n) => n.type === 'start')] }
    const repaired = ensureCoreNodes(duplicated)
    expect(repaired.nodes.filter((n) => n.type === 'start').length).toBe(1)
    expect(repaired.nodes.length).toBe(full.nodes.length)
  })

  it('ensureCoreNodes：完整画布原样返回', () => {
    const editor = toEditorFormat(buildDefinition())
    expect(ensureCoreNodes(editor)).toBe(editor)
  })

  it('collectUpstreamVariables 提供 sys 与祖先节点输出', () => {
    const editor = toEditorFormat(buildDefinition())
    const options = collectUpstreamVariables(editor, 'check')
    const values = options.map((o) => o.value)
    expect(values).toContain('sys.instanceId')
    expect(values).toContain('start.question')
    expect(values).toContain('search.hasResult')
    // end 不是 check 的上游
    expect(values.some((v) => v.startsWith('end.'))).toBe(false)
    // 非上游选项在 end 节点上包含所有其它节点
    const endOptions = collectUpstreamVariables(editor, 'end').map((o) => o.value)
    expect(endOptions).toContain('answer.answer')
    expect(endOptions).toContain('fallback.answer')
  })

  it('normalizeEditorData 把节点内 edges 提升到顶层（乱序目标不丢失）', () => {
    // 模拟旧格式：连线挂在源节点内，且目标节点排在源节点之后（FlowGram 会静默丢弃的场景）
    const editor = toEditorFormat(buildDefinition())
    const nodes = editor.nodes.map((n) => ({ ...n, edges: [] as import('../types').EditorEdgeJSON[] }))
    // check 的出边依赖后面的 answer/fallback
    const check = nodes.find((n) => n.id === 'check')!
    check.edges = [
      { sourceNodeID: 'check', targetNodeID: 'answer', sourcePortID: 'true' },
      { sourceNodeID: 'check', targetNodeID: 'fallback', sourcePortID: 'false' },
    ]
    const normalized = normalizeEditorData({ nodes, edges: [] })
    expect(normalized.nodes.every((n) => !n.edges)).toBe(true)
    const ports = normalized.edges.filter((e) => e.sourceNodeID === 'check').map((e) => String(e.sourcePortID)).sort()
    expect(ports).toEqual(['false', 'true'])
    // 加载端：顶层 edges 全部保留（此场景只有 check 的两条出边）
    expect(normalized.edges).toHaveLength(2)
  })

  it('开始节点声明映射：inputs(声明) ↔ outputs(启动参数校验)', () => {
    const editor = toEditorFormat(buildDefinition())
    // toEditorFormat：start 的 outputs 声明映射为 data.inputs（run 形态）
    const start = editor.nodes.find((n) => n.id === 'start')!
    const decl = start.data!.inputs!.question!
    expect(decl.expressionType).toBe('run')
    expect(decl.required).toBe(true)
    expect(decl.fieldType).toBe('string')

    // fromEditorFormat：声明映射回 outputs 供引擎校验，definition.inputs 置空
    const restored = fromEditorFormat(editor, 'x')
    const startDef = restored.nodes.find((n) => n.key === 'start')!
    expect(startDef.inputs).toEqual({})
    expect(startDef.outputs).toHaveLength(1)
    expect(startDef.outputs[0]).toMatchObject({ name: 'question', fieldType: 'string', isRequired: true })
  })

  it('toEditorFormat：旧定义自定义开始参数收敛为固定 question', () => {
    const legacy = buildDefinition()
    legacy.nodes[0]!.outputs = [
      { name: 'query', fieldType: 'string', isRequired: true },
      { name: 'foo', fieldType: 'string', isRequired: false },
    ]
    const legacyEditor = toEditorFormat(legacy)
    const start = legacyEditor.nodes.find((n) => n.type === 'start')!
    expect(Object.keys(start.data!.inputs!)).toEqual(['question'])
    expect(start.data!.inputs!.question).toMatchObject({ expressionType: 'run', required: true, fieldType: 'string' })
  })

  it('fromEditorFormat：开始节点忽略画布声明固定输出 question（不可自定义输入/输出字段）', () => {
    const editor = createDefaultEditorData()
    const start = editor.nodes.find((n) => n.type === 'start')!
    start.data = { ...start.data, inputs: { foo: { expressionType: 'run', value: '', required: true, fieldType: 'string' } } }
    const def = fromEditorFormat(editor, 'x')
    const startDef = def.nodes.find((n) => n.type === 'start')!
    expect(startDef.inputs).toEqual({})
    expect(startDef.outputs).toEqual([{ name: 'question', fieldType: 'string', isRequired: true }])
  })

  it('convergeStartContract：旧草稿画布收敛固定 question 并迁移 start.query 引用', () => {
    // 旧草稿：start 声明自定义 query/foo 参数，下游以 start<key>.query 引用（变量/插值/脚本/分支）
    const editor = createDefaultEditorData()
    const start = editor.nodes.find((n) => n.type === 'start')!
    const key = start.id
    start.data = {
      ...start.data,
      inputs: {
        query: { expressionType: 'run', value: '', required: true, fieldType: 'string' },
        foo: { expressionType: 'run', value: '', required: false, fieldType: 'string' },
      },
    }
    const end = editor.nodes.find((n) => n.type === 'end')!
    end.data = {
      ...end.data,
      inputs: {
        output: { expressionType: 'variable', value: `${key}.query`, required: false },
        raw: { expressionType: 'variable', value: `${key}.queryExtra`, required: false },
        custom: { expressionType: 'variable', value: `${key}.foo`, required: false },
      },
    }
    editor.nodes.push({
      id: 'js1',
      type: 'javaScript',
      blocks: [],
      edges: [],
      data: {
        title: 'JS',
        content: '',
        inputs: {},
        outputs: [{ name: 'result', fieldType: 'string' }],
        settings: { code: `return { q: nodes.${key}.query }` },
      },
    })
    editor.nodes.push({
      id: 'http1',
      type: 'http',
      blocks: [],
      edges: [],
      data: {
        title: 'HTTP',
        content: '',
        inputs: {},
        outputs: [],
        settings: { method: 'POST', url: `https://x.dev/q?k={${key}.query}`, body: `{"q":"{${key}.query}"}` },
      },
    })
    editor.nodes.push({
      id: 'sw1',
      type: 'switch',
      blocks: [],
      edges: [],
      data: {
        title: '多条件',
        content: '',
        inputs: {},
        outputs: [],
        branches: [{ id: 'b1', label: '条件 1', binding: { expressionType: 'variable', value: `${key}.query`, required: true } }],
      },
    })

    const converged = convergeStartContract(editor)
    const startNode = converged.nodes.find((n) => n.type === 'start')!
    expect(Object.keys(startNode.data!.inputs!)).toEqual(['question'])
    expect(startNode.data!.inputs!.question).toMatchObject({ expressionType: 'run', required: true, fieldType: 'string' })

    const endNode = converged.nodes.find((n) => n.type === 'end')!
    expect(endNode.data!.inputs!.output!.value).toBe(`${key}.question`)
    // queryExtra/foo 不是 query，不迁移
    expect(endNode.data!.inputs!.raw!.value).toBe(`${key}.queryExtra`)
    expect(endNode.data!.inputs!.custom!.value).toBe(`${key}.foo`)

    const js = converged.nodes.find((n) => n.id === 'js1')!
    expect(js.data!.settings!.code).toBe(`return { q: nodes.${key}.question }`)
    const http = converged.nodes.find((n) => n.id === 'http1')!
    expect(http.data!.settings!.url).toBe(`https://x.dev/q?k={${key}.question}`)
    expect(http.data!.settings!.body).toBe(`{"q":"{${key}.question}"}`)
    const sw = converged.nodes.find((n) => n.id === 'sw1')!
    expect(sw.data!.branches![0].binding.value).toBe(`${key}.question`)

    // 收敛幂等；再保存时 start 输出即固定 question 契约
    expect(convergeStartContract(converged)).toEqual(converged)
    const def = fromEditorFormat(converged, 'x')
    const startDef = def.nodes.find((n) => n.type === 'start')!
    expect(startDef.outputs).toEqual([{ name: 'question', fieldType: 'string', isRequired: true }])
    expect(def.nodes.find((n) => n.key === 'end')?.inputs.output.value).toBe(`${key}.question`)
  })

  it('convergeStartContract：无开始节点时原样返回', () => {
    const editor = createDefaultEditorData()
    editor.nodes = editor.nodes.filter((n) => n.type !== 'start')
    expect(convergeStartContract(editor)).toEqual(editor)
  })

  it('aiChat：模型/系统提示词/温度随保存进入引擎 config，越界温度丢弃', () => {
    const editor = toEditorFormat(buildDefinition())
    const ai = editor.nodes.find((n) => n.type === 'aiChat')!
    ai.data = {
      ...ai.data,
      settings: { aiModelId: 'model-1', systemPrompt: '你是严谨的助手', temperature: 0.7 },
    }
    const def = fromEditorFormat(editor, 'x')
    const aiDef = def.nodes.find((n) => n.type === 'aiChat')!
    expect(aiDef.config).toMatchObject({ aiModelId: 'model-1', systemPrompt: '你是严谨的助手', temperature: 0.7 })

    // 温度越界（>2）被清洗丢弃
    ai.data = { ...ai.data, settings: { aiModelId: 'model-1', temperature: 3 } }
    const def2 = fromEditorFormat(editor, 'x')
    expect((def2.nodes.find((n) => n.type === 'aiChat')?.config ?? {}) as Record<string, unknown>).not.toHaveProperty('temperature')
  })

  it('aiChat：历史草稿残留的 skillIds/sandboxEnabled 随保存清洗丢弃', () => {
    const editor = toEditorFormat(buildDefinition())
    const ai = editor.nodes.find((n) => n.type === 'aiChat')!
    ai.data = {
      ...ai.data,
      settings: {
        aiModelId: 'model-1',
        skillIds: ['11111111-2222-3333-4444-555555555555'],
        sandboxEnabled: true,
      } as import('../types').NodeSettings,
    }
    const def = fromEditorFormat(editor, 'x')
    const config = (def.nodes.find((n) => n.type === 'aiChat')?.config ?? {}) as Record<string, unknown>
    expect(config).not.toHaveProperty('skillIds')
    expect(config).not.toHaveProperty('sandboxEnabled')
  })

  it('agentApp：应用 id 随保存进入引擎 config，未选择应用保存校验报错', () => {
    const editor = createDefaultEditorData()
    const appId = 'aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee'
    editor.nodes.splice(1, 0, {
      id: 'agent1',
      type: 'agentApp',
      blocks: [],
      edges: [],
      data: {
        title: '专家',
        content: '',
        inputs: { prompt: { expressionType: 'variable', value: 'start.question', required: true } },
        outputs: [{ name: 'answer', fieldType: 'string' }],
        settings: { agentAppId: appId },
      },
    })
    // 连线 start → agent1 → end（清掉默认画布的节点内连线，避免出边重复）
    editor.nodes.forEach((n) => { n.edges = [] })
    editor.edges = [
      { sourceNodeID: 'start', targetNodeID: 'agent1' },
      { sourceNodeID: 'agent1', targetNodeID: 'end' },
    ]
    expect(validateEditorData(editor)).toEqual([])
    const def = fromEditorFormat(editor, 'x')
    expect(def.nodes.find((n) => n.type === 'agentApp')?.config).toMatchObject({ agentAppId: appId })

    // 未选择应用 → 校验报错并指明节点名
    const agent = editor.nodes.find((n) => n.type === 'agentApp')!
    agent.data = { ...agent.data, settings: {} }
    const errors = validateEditorData(editor)
    const hit = errors.find((e) => e.message.includes('未选择应用'))
    expect(hit).toBeDefined()
    expect(hit!.message).toBe('Agent 应用节点「专家」未选择应用，请在节点配置中选择 Agent 应用')
  })

  it('aiChat：未配置模型保存校验报错并指明节点名', () => {
    const editor = toEditorFormat(buildDefinition())
    const ai = editor.nodes.find((n) => n.type === 'aiChat')!
    ai.data = { ...ai.data, title: 'AI 回答', settings: { ...ai.data?.settings, aiModelId: '' } }
    const errors = validateEditorData(editor)
    const hit = errors.find((e) => e.message.includes('未配置模型'))
    expect(hit).toBeDefined()
    expect(hit!.message).toBe('AI 对话节点「AI 回答」未配置模型，请在节点配置中选择 AI 模型')
    expect(hit!.nodeId).toBe(ai.id)
  })

  it('输入绑定保留 fieldType 元数据', () => {
    const editor = toEditorFormat(buildDefinition())
    // 用非 start 节点测（start 的 inputs 是启动参数声明，另有专属映射）
    editor.nodes[1].data!.inputs = {
      q: { expressionType: 'fixed', value: 'x', fieldType: 'string' },
    }
    const restored = fromEditorFormat(editor, 'x')
    expect(restored.nodes[1].inputs.q.fieldType).toBe('string')
  })
})

describe('workflow validation', () => {
  it('缺少开始节点报错', () => {
    const editor = toEditorFormat(buildDefinition())
    editor.nodes = editor.nodes.filter((n) => n.type !== 'start')
    // 连带清理指向 start 的边
    editor.edges = (editor.edges ?? []).filter((e) => e.sourceNodeID !== 'start')
    const errors = validateEditorData(editor)
    expect(errors.some((e) => e.message.includes('开始节点'))).toBe(true)
  })

  it('画上画的连线（顶层 edges）计入校验与连接', () => {
    // 模拟 toJSON 行为：画布上新画的连线在顶层 edges，节点内 edges 为空
    const editor = createDefaultEditorData()
    editor.nodes.forEach((n) => { n.edges = [] })
    editor.edges = [{ sourceNodeID: editor.nodes[0].id, targetNodeID: editor.nodes[1].id }]
    expect(validateEditorData(editor)).toEqual([])
    const restored = fromEditorFormat(editor, 'x')
    expect(restored.connections).toHaveLength(1)
    expect(restored.connections[0].source).toBe(editor.nodes[0].id)
  })

  it('条件节点缺少 false 出边报错', () => {
    const editor = toEditorFormat(buildDefinition())
    editor.edges = (editor.edges ?? []).filter((e) => !(e.sourceNodeID === 'check' && String(e.sourcePortID) === 'false'))
    const errors = validateEditorData(editor)
    expect(errors.some((e) => e.message.includes('false'))).toBe(true)
  })

  it('环路报错', () => {
    const editor = createDefaultEditorData()
    const [start, end] = editor.nodes
    // start → end → start 构成环
    end.edges = [{ sourceNodeID: end.id, targetNodeID: start.id }]
    const errors = validateEditorData(editor)
    expect(errors.some((e) => e.message.includes('环路'))).toBe(true)
  })

  it('变量引用非上游节点报错', () => {
    const editor = createDefaultEditorData()
    const start = editor.nodes.find((n) => n.type === 'start')
    start!.data = {
      ...start!.data,
      inputs: { x: { expressionType: 'variable', value: 'end.output' } },
    }
    const errors = validateEditorData(editor)
    expect(errors.some((e) => e.message.includes('非上游节点'))).toBe(true)
  })

  it('删除节点后残留引用报错并指明所在节点（含节点名）', () => {
    // 复现：删除条件/fallback 分支节点后，结束节点仍保留 fallback 输入绑定
    const editor = createDefaultEditorData()
    const end = editor.nodes.find((n) => n.type === 'end')!
    end.data = {
      ...end.data,
      title: '结束',
      inputs: {
        answer: { expressionType: 'variable', value: 'start.question', required: false },
        fallback: { expressionType: 'variable', value: 'fallback.answer', required: false },
      },
    }
    const errors = validateEditorData(editor)
    const hit = errors.find((e) => e.message.includes('不存在的节点'))
    expect(hit).toBeDefined()
    expect(hit!.message).toBe('节点「结束」的输入 fallback 引用了不存在的节点：fallback.answer')
    expect(hit!.nodeId).toBe(end.id)
  })
})

describe('节点 Key（data.key 覆盖）', () => {
  /** 带自定义 key 的画布：js_1 改名为 lookup、cond_1 改名为 check，引用仍写旧 id（改 key 未重载的场景） */
  const keyEditor = (): import('../types').EditorWorkflowJSON => ({
    nodes: [
      { id: 'start', type: 'start', data: { title: '开始', inputs: { question: { expressionType: 'run', value: '', required: true, fieldType: 'string' } } }, blocks: [], edges: [{ sourceNodeID: 'start', targetNodeID: 'js_1' }] },
      { id: 'js_1', type: 'javaScript', data: { key: 'lookup', title: '检索', outputs: [{ name: 'summary', fieldType: 'string' }] }, blocks: [], edges: [{ sourceNodeID: 'js_1', targetNodeID: 'cond_1' }] },
      { id: 'cond_1', type: 'condition', data: { key: 'check', inputs: { condition: { expressionType: 'variable', value: 'js_1.summary', required: true } } }, blocks: [], edges: [{ sourceNodeID: 'cond_1', targetNodeID: 'end', sourcePortID: 'true' }, { sourceNodeID: 'cond_1', targetNodeID: 'end', sourcePortID: 'false' }] },
      { id: 'end', type: 'end', data: { inputs: { result: { expressionType: 'variable', value: 'check.result', required: false } } }, blocks: [], edges: [] },
    ],
    edges: [],
  })

  it('fromEditorFormat：引擎 key 取 data.key，连线/坐标按 key 换算，引用重写为新 key', () => {
    const def = fromEditorFormat(keyEditor(), 'x')
    expect(def.nodes.map((n) => n.key)).toEqual(['start', 'lookup', 'check', 'end'])
    // 旧 id 前缀的引用被重写为新 key
    expect(def.nodes.find((n) => n.key === 'check')?.inputs.condition.value).toBe('lookup.summary')
    // 连线 source/target 用引擎 key
    expect(def.connections.map((c) => [c.source, c.target]).slice(0, 2)).toEqual([['start', 'lookup'], ['lookup', 'check']])
    expect(def.connections.every((c) => c.id.startsWith('c_'))).toBe(true)
    // 坐标按 key 落位
    const def2 = { ...def, ui: { nodePositions: { ...def.ui?.nodePositions, lookup: { x: 9, y: 9 } } } }
    const editor2 = toEditorFormat(def2)
    expect(editor2.nodes.find((n) => n.id === 'lookup')?.meta?.position).toEqual({ x: 9, y: 9 })
  })

  it('改 key 的画布通过校验（引用按 id 或 key 都可回溯）', () => {
    expect(validateEditorData(keyEditor())).toEqual([])
  })

  it('collectUpstreamVariables 提示值使用有效 key', () => {
    const options = collectUpstreamVariables(keyEditor(), 'end').map((o) => o.value)
    expect(options).toContain('lookup.summary')
    expect(options).toContain('start.question')
  })

  it('validateEditorData 拦截重复/非法/保留字 Key', () => {
    const editor = keyEditor()
    const js = editor.nodes.find((n) => n.id === 'js_1')!
    const cond = editor.nodes.find((n) => n.id === 'cond_1')!
    // 重复
    cond.data!.key = 'lookup'
    expect(validateEditorData(editor).some((e) => e.message.includes('重复的节点 Key'))).toBe(true)
    // 非法字符
    js.data!.key = 'a-b'
    cond.data!.key = 'check'
    expect(validateEditorData(editor).some((e) => e.message.includes('Key 只能'))).toBe(true)
    // 保留字
    js.data!.key = 'sys'
    expect(validateEditorData(editor).some((e) => e.message.includes('保留字'))).toBe(true)
  })
})

describe('normalizeReferences（画布快照引用规范化）', () => {
  it('把旧 id 前缀的 variable/interpolation 引用改写为有效 key', () => {
    const editor: import('../types').EditorWorkflowJSON = {
      nodes: [
        { id: 'js_1', type: 'javaScript', data: { key: 'lookup', outputs: [{ name: 'summary', fieldType: 'string' }] }, blocks: [], edges: [] },
        {
          id: 'cond_1',
          type: 'condition',
          data: {
            inputs: {
              condition: { expressionType: 'variable', value: 'js_1.summary', required: true },
              tpl: { expressionType: 'interpolation', value: '结果：{js_1.summary}', required: false },
              fixed: { expressionType: 'fixed', value: 'js_1.summary', required: false },
            },
          },
          blocks: [],
          edges: [],
        },
      ],
      edges: [],
    }
    const normalized = normalizeReferences(editor)
    const inputs = normalized.nodes[1].data!.inputs!
    expect(inputs.condition.value).toBe('lookup.summary')
    expect(inputs.tpl.value).toBe('结果：{lookup.summary}')
    // fixed 值是字面量，不参与重写
    expect(inputs.fixed.value).toBe('js_1.summary')
    // 重复规范化幂等（内容不变）
    expect(normalizeReferences(normalized)).toStrictEqual(normalized)
  })
})

describe('多条件节点（switch）', () => {
  const switchEditor = (): import('../types').EditorWorkflowJSON => ({
    nodes: [
      { id: 'start', type: 'start', data: { inputs: { flag: { expressionType: 'run', value: '', required: true, fieldType: 'boolean' } } }, blocks: [], edges: [{ sourceNodeID: 'start', targetNodeID: 'sw' }] },
      { id: 'sw', type: 'switch', data: { branches: [
        { id: 'b1', label: '条件 1', binding: { expressionType: 'variable', value: 'start.question', required: true } },
        { id: 'b2', label: '条件 2', binding: { expressionType: 'fixed', value: 'true', required: true } },
      ] }, blocks: [], edges: [
        { sourceNodeID: 'sw', targetNodeID: 'end', sourcePortID: 'b1' },
        { sourceNodeID: 'sw', targetNodeID: 'end', sourcePortID: 'else' },
      ] },
      { id: 'end', type: 'end', data: {}, blocks: [], edges: [] },
    ],
    edges: [],
  })

  it('fromEditorFormat：branches 映射为 config.branches，出边携带分支标记', () => {
    const def = fromEditorFormat(switchEditor(), 'x')
    expect(def.nodes.find((n) => n.key === 'sw')?.config).toEqual({
      branches: [
        { id: 'b1', label: '条件 1', binding: { expressionType: 'variable', value: 'start.question', required: true } },
        { id: 'b2', label: '条件 2', binding: { expressionType: 'fixed', value: 'true', required: true } },
      ],
    })
    expect(def.connections.filter((c) => c.source === 'sw').map((c) => c.condition)).toEqual(['b1', 'else'])
  })

  it('toEditorFormat 往返：config.branches 还原为 data.branches', () => {
    const editor2 = toEditorFormat(fromEditorFormat(switchEditor(), 'x'))
    const sw = editor2.nodes.find((n) => n.id === 'sw')
    expect(sw?.data?.branches?.map((b) => b.id)).toEqual(['b1', 'b2'])
  })

  it('validateEditorData：无效/重复分支标记报错，合法通过', () => {
    expect(validateEditorData(switchEditor())).toEqual([])
    const bad = switchEditor()
    const sw = bad.nodes.find((n) => n.id === 'sw')!
    sw.edges = [
      { sourceNodeID: 'sw', targetNodeID: 'end', sourcePortID: 'nope' },
      { sourceNodeID: 'sw', targetNodeID: 'start', sourcePortID: 'nope' },
    ]
    const errors = validateEditorData(bad)
    expect(errors.some((e) => e.message.includes('分支标记无效'))).toBe(true)
    expect(errors.some((e) => e.message.includes('重复的分支出边'))).toBe(true)
  })
})

describe('团队插件请求 schema 转输入绑定', () => {
  it('inputsFromPluginSchema：fixed 空值绑定 + 类型/描述，剔除无名项', () => {
    const inputs = inputsFromPluginSchema([
      { name: 'url', fieldType: 'string', description: '目标网页链接' },
      { name: 'extractText', fieldType: 'boolean' },
      { name: '' },
    ])
    expect(inputs.url).toEqual({
      expressionType: 'fixed',
      value: '',
      required: true,
      fieldType: 'string',
      description: '目标网页链接',
    })
    expect(inputs.extractText).toMatchObject({ expressionType: 'fixed', fieldType: 'boolean' })
    expect(Object.keys(inputs)).toEqual(['url', 'extractText'])
    expect(inputsFromPluginSchema(null)).toEqual({})
  })
})

describe('团队插件响应 schema 转输出声明', () => {
  it('outputsFromPluginSchema：字段映射，剔除无名项，空 schema 返回空数组', () => {
    const outputs = outputsFromPluginSchema([
      { name: 'answer', fieldType: 'string', description: '回答内容' },
      { name: '', fieldType: 'string' },
      { fieldType: 'number' },
      { name: 'docs', fieldType: 'array', description: '文档列表' },
    ])
    expect(outputs).toEqual([
      { name: 'answer', fieldType: 'string', description: '回答内容' },
      { name: 'docs', fieldType: 'array', description: '文档列表' },
    ])
    expect(outputsFromPluginSchema([])).toEqual([])
    expect(outputsFromPluginSchema(null)).toEqual([])
  })
})

describe('输出连线限制（单出边）', () => {
  it('普通节点多条出边报错，条件节点多出边放行', () => {
    const editor: import('../types').EditorWorkflowJSON = {
      nodes: [
        { id: 'start', type: 'start', data: { inputs: {} }, blocks: [], edges: [{ sourceNodeID: 'start', targetNodeID: 'js' }] },
        { id: 'js', type: 'javaScript', data: { settings: { code: 'function run() {}' } }, blocks: [], edges: [
          { sourceNodeID: 'js', targetNodeID: 'end1' },
          { sourceNodeID: 'js', targetNodeID: 'end2' },
        ] },
        { id: 'end1', type: 'end', data: {}, blocks: [], edges: [] },
        { id: 'end2', type: 'end', data: {}, blocks: [], edges: [] },
      ],
      edges: [],
    }
    const errors = validateEditorData(editor)
    expect(errors.some((e) => e.nodeId === 'js' && e.message.includes('只允许一条输出连线'))).toBe(true)

    // 条件节点双出边不受限制
    const ok: import('../types').EditorWorkflowJSON = {
      nodes: [
        { id: 'start', type: 'start', data: { inputs: {} }, blocks: [], edges: [{ sourceNodeID: 'start', targetNodeID: 'check' }] },
        { id: 'check', type: 'condition', data: { inputs: {} }, blocks: [], edges: [
          { sourceNodeID: 'check', targetNodeID: 'end', sourcePortID: 'true' },
          { sourceNodeID: 'check', targetNodeID: 'end', sourcePortID: 'false' },
        ] },
        { id: 'end', type: 'end', data: {}, blocks: [], edges: [] },
      ],
      edges: [],
    }
    expect(validateEditorData(ok)).toEqual([])
  })
})

describe('问题分类节点（questionClassifier）', () => {
  const clfEditor = (): import('../types').EditorWorkflowJSON => ({
    nodes: [
      { id: 'start', type: 'start', data: { inputs: { question: { expressionType: 'run', value: '', required: true, fieldType: 'string' } } }, blocks: [], edges: [{ sourceNodeID: 'start', targetNodeID: 'clf' }] },
      {
        id: 'clf',
        type: 'questionClassifier',
        data: {
          title: '问题分类',
          inputs: {
            query: { expressionType: 'variable', value: 'start.question', required: true },
            history: { expressionType: 'variable', value: 'start.history', required: false },
          },
          outputs: [
            { name: 'result', fieldType: 'string' },
            { name: 'className', fieldType: 'string' },
          ],
          // junkKey 为故意传入的未知属性，用于断言序列化时被丢弃
          settings: { aiModelId: 'm1', backgroundKnowledge: '商城知识', historyCount: 4, junkKey: 'x' } as import('../types').NodeSettings,
          classes: [
            { id: 'c1', label: '售前咨询' },
            { id: 'c2', label: '售后咨询' },
          ],
        },
        blocks: [],
        edges: [
          { sourceNodeID: 'clf', targetNodeID: 'a1', sourcePortID: 'c1' },
          { sourceNodeID: 'clf', targetNodeID: 'a2', sourcePortID: 'c2' },
        ],
      },
      { id: 'a1', type: 'javaScript', data: { settings: { code: 'function run() {}' } }, blocks: [], edges: [{ sourceNodeID: 'a1', targetNodeID: 'end' }] },
      { id: 'a2', type: 'javaScript', data: { settings: { code: 'function run() {}' } }, blocks: [], edges: [{ sourceNodeID: 'a2', targetNodeID: 'end' }] },
      { id: 'end', type: 'end', data: {}, blocks: [], edges: [] },
    ],
    edges: [],
  })

  it('nodeDataFromTemplate 提供默认分类与设置', () => {
    const data = nodeDataFromTemplate('questionClassifier')
    expect(data?.classes?.map((c) => c.id)).toEqual(['c1', 'c2'])
    expect(data?.settings?.historyCount).toBe(6)
    expect(data?.settings?.aiModelId).toBe('')
    expect(data?.inputs?.query.required).toBe(true)
    expect(data?.inputs?.history.required).toBe(false)
    expect(data?.outputs?.map((o) => o.name)).toEqual(['result', 'className'])
  })

  it('fromEditorFormat：classes 映射为 config.classes，出边携带分类标记，settings 清洗', () => {
    const def = fromEditorFormat(clfEditor(), 'x')
    const clf = def.nodes.find((n) => n.key === 'clf')!
    expect(clf.config).toEqual({
      aiModelId: 'm1',
      backgroundKnowledge: '商城知识',
      historyCount: 4,
      classes: [
        { id: 'c1', label: '售前咨询' },
        { id: 'c2', label: '售后咨询' },
      ],
    })
    expect(def.connections.filter((c) => c.source === 'clf').map((c) => c.condition)).toEqual(['c1', 'c2'])
    // 输入绑定保留（含可选的历史消息变量绑定）
    expect(clf.inputs.history).toMatchObject({ value: 'start.history', required: false })
  })

  it('toEditorFormat 往返：config.classes 还原为 data.classes，设置从 config 摘出', () => {
    const editor2 = toEditorFormat(fromEditorFormat(clfEditor(), 'x'))
    const clf = editor2.nodes.find((n) => n.id === 'clf')
    expect(clf?.type).toBe('questionClassifier')
    expect(clf?.data?.classes?.map((c) => c.id)).toEqual(['c1', 'c2'])
    expect(clf?.data?.settings).toEqual({ aiModelId: 'm1', backgroundKnowledge: '商城知识', historyCount: 4 })
    expect(clf?.data?.inputs?.query.value).toBe('start.question')
    expect(validateEditorData(editor2)).toEqual([])
  })

  it('validateEditorData：分类为空/分类名为空/标记无效或重复报错，合法通过', () => {
    expect(validateEditorData(clfEditor())).toEqual([])

    const noClasses = clfEditor()
    noClasses.nodes.find((n) => n.id === 'clf')!.data!.classes = []
    expect(validateEditorData(noClasses).some((e) => e.message.includes('至少需要配置一个分类'))).toBe(true)

    const emptyLabel = clfEditor()
    emptyLabel.nodes.find((n) => n.id === 'clf')!.data!.classes![1].label = '  '
    expect(validateEditorData(emptyLabel).some((e) => e.message.includes('分类值不可为空'))).toBe(true)

    const badMarker = clfEditor()
    badMarker.nodes.find((n) => n.id === 'clf')!.edges![1].sourcePortID = 'nope'
    expect(validateEditorData(badMarker).some((e) => e.message.includes('分类标记无效'))).toBe(true)

    const dupMarker = clfEditor()
    dupMarker.nodes.find((n) => n.id === 'clf')!.edges![1].sourcePortID = 'c1'
    expect(validateEditorData(dupMarker).some((e) => e.message.includes('重复的分类出边'))).toBe(true)
  })
})

describe('知识库检索节点（knowledgeSearch）', () => {
  const ksEditor = (): import('../types').EditorWorkflowJSON => ({
    nodes: [
      { id: 'start', type: 'start', data: { inputs: { question: { expressionType: 'run', value: '', required: true, fieldType: 'string' } } }, blocks: [], edges: [{ sourceNodeID: 'start', targetNodeID: 'ks' }] },
      {
        id: 'ks',
        type: 'knowledgeSearch',
        data: {
          title: '检索知识库',
          inputs: {
            query: { expressionType: 'variable', value: 'start.question', required: true },
            wikiId: { expressionType: 'variable', value: 'start.wikiId', required: false },
          },
          outputs: [
            { name: 'query', fieldType: 'string' },
            { name: 'hits', fieldType: 'array' },
          ],
          // junkKey 为故意传入的未知属性，用于断言序列化时被丢弃
          settings: { wikiId: 7, topK: 9, junkKey: 'should.be.dropped' } as import('../types').NodeSettings,
        },
        blocks: [],
        edges: [{ sourceNodeID: 'ks', targetNodeID: 'end' }],
      },
      { id: 'end', type: 'end', data: {}, blocks: [], edges: [] },
    ],
    edges: [],
  })

  it('nodeDataFromTemplate 提供默认知识库配置与输出声明', () => {
    const data = nodeDataFromTemplate('knowledgeSearch')
    expect(data?.settings?.topK).toBe(5)
    expect(data?.inputs?.query.required).toBe(true)
    // 知识库变量绑定为可选输入（单个知识库，运行时优先于静态选择）
    expect(data?.inputs?.wikiId?.required).toBe(false)
    expect(data?.outputs?.map((o) => o.name)).toEqual(['query', 'count', 'hits', 'contents', 'text'])
  })

  it('fromEditorFormat：settings 清洗为 wikiId/topK，剔除非法与未知键', () => {
    const def = fromEditorFormat(ksEditor(), 'x')
    const ks = def.nodes.find((n) => n.key === 'ks')!
    expect(ks.config).toEqual({ wikiId: 7, topK: 9 })
    // 输入绑定保留（含可选的 wikiId 变量绑定）
    expect(ks.inputs.wikiId).toMatchObject({ value: 'start.wikiId', required: false })
  })

  it('toEditorFormat 往返保留 knowledgeSearch 节点与绑定', () => {
    const editor2 = toEditorFormat(fromEditorFormat(ksEditor(), 'x'))
    const ks = editor2.nodes.find((n) => n.id === 'ks')
    expect(ks?.type).toBe('knowledgeSearch')
    expect(ks?.data?.settings).toEqual({ wikiId: 7, topK: 9 })
    expect(ks?.data?.inputs?.query.value).toBe('start.question')
    expect(ks?.data?.inputs?.wikiId?.value).toBe('start.wikiId')
    expect(validateEditorData(editor2)).toEqual([])
  })
})

describe('知识图谱检索节点（kgSearch）', () => {
  const kgEditor = (): import('../types').EditorWorkflowJSON => ({
    nodes: [
      { id: 'start', type: 'start', data: { inputs: { question: { expressionType: 'run', value: '', required: true, fieldType: 'string' } } }, blocks: [], edges: [{ sourceNodeID: 'start', targetNodeID: 'kg' }] },
      {
        id: 'kg',
        type: 'kgSearch',
        data: {
          title: '检索知识图谱',
          inputs: {
            query: { expressionType: 'variable', value: 'start.question', required: true },
          },
          outputs: [
            { name: 'query', fieldType: 'string' },
            { name: 'text', fieldType: 'string' },
          ],
          // junkKey 为故意传入的未知属性，用于断言序列化时被丢弃
          settings: { graphId: 11, topK: 3, junkKey: 'should.be.dropped' } as import('../types').NodeSettings,
        },
        blocks: [],
        edges: [{ sourceNodeID: 'kg', targetNodeID: 'end' }],
      },
      { id: 'end', type: 'end', data: {}, blocks: [], edges: [] },
    ],
    edges: [],
  })

  it('nodeDataFromTemplate 提供默认图谱配置与五项输出声明', () => {
    const data = nodeDataFromTemplate('kgSearch')
    // 静态图谱 id 缺省未选（v1 不做变量绑定），召回条数默认 5
    expect(data?.settings?.topK).toBe(5)
    expect(data?.settings?.graphId).toBeUndefined()
    // kgSearch 只保留 query 一个输入（图谱 id 为静态配置，不走输入绑定）
    expect(Object.keys(data?.inputs ?? {})).toEqual(['query'])
    expect(data?.inputs?.query.required).toBe(true)
    expect(data?.outputs?.map((o) => o.name)).toEqual(['query', 'count', 'hits', 'contents', 'text'])
  })

  it('fromEditorFormat：settings 清洗为 graphId/topK，剔除非法与未知键', () => {
    const def = fromEditorFormat(kgEditor(), 'x')
    const kg = def.nodes.find((n) => n.key === 'kg')!
    expect(kg.config).toEqual({ graphId: 11, topK: 3 })
    expect(kg.inputs.query).toMatchObject({ value: 'start.question', required: true })
  })

  it('graphId 非法（0/负数/非整数）时被丢弃', () => {
    for (const bad of [0, -3, 2.5]) {
      const editor = kgEditor()
      const kgNode = editor.nodes.find((n) => n.id === 'kg')!
      kgNode.data = { ...kgNode.data, settings: { graphId: bad, topK: 3 } as import('../types').NodeSettings }
      const def = fromEditorFormat(editor, 'x')
      const kg = def.nodes.find((n) => n.key === 'kg')!
      expect(kg.config).toEqual({ topK: 3 })
    }
  })

  it('toEditorFormat 往返保留 kgSearch 节点与绑定', () => {
    const editor2 = toEditorFormat(fromEditorFormat(kgEditor(), 'x'))
    const kg = editor2.nodes.find((n) => n.id === 'kg')
    expect(kg?.type).toBe('kgSearch')
    expect(kg?.data?.settings).toEqual({ graphId: 11, topK: 3 })
    expect(kg?.data?.inputs?.query.value).toBe('start.question')
    expect(validateEditorData(editor2)).toEqual([])
  })
})

describe('HTTP 请求节点（http）', () => {
  const httpEditor = (): import('../types').EditorWorkflowJSON => ({
    nodes: [
      { id: 'start', type: 'start', data: { inputs: { question: { expressionType: 'run', value: '', required: true, fieldType: 'string' } } }, blocks: [], edges: [{ sourceNodeID: 'start', targetNodeID: 'http1' }] },
      {
        id: 'http1',
        type: 'http',
        data: {
          title: '调用外部接口',
          inputs: {},
          outputs: [
            { name: 'statusCode', fieldType: 'number' },
            { name: 'rawResponse', fieldType: 'dynamic' },
            { name: 'hasError', fieldType: 'boolean' },
            { name: 'errorMessage', fieldType: 'string' },
            { name: 'title', fieldType: 'string' },
          ],
          settings: {
            method: 'post',
            url: 'https://api.example.com/{start.question}',
            timeoutSeconds: 60,
            params: [{ name: 'q', value: '{start.question}' }, { name: '', value: 'dropped' }],
            headers: [{ name: 'X-Trace', value: 't-1' }],
            bodyType: 'json',
            body: '{"q":"{start.question}"}',
            auth: { type: 'bearer', token: 'tk-1' },
            errorCapture: true,
            extract: [
              { name: 'title', path: '$.data.title', fieldType: 'string' },
              { name: '', path: '$.x', fieldType: 'string' },
            ],
            // junkKey 为故意传入的未知属性，用于断言序列化时被丢弃
            junkKey: 'should.be.dropped',
          } as import('../types').NodeSettings,
        },
        blocks: [],
        edges: [{ sourceNodeID: 'http1', targetNodeID: 'end' }],
      },
      { id: 'end', type: 'end', data: {}, blocks: [], edges: [] },
    ],
    edges: [],
  })

  it('nodeDataFromTemplate 提供默认请求配置与固定输出声明', () => {
    const data = nodeDataFromTemplate('http')
    expect(data?.settings?.method).toBe('GET')
    expect(data?.settings?.timeoutSeconds).toBe(30)
    expect(data?.settings?.params).toEqual([])
    expect(data?.outputs?.map((o) => o.name)).toEqual(['statusCode', 'rawResponse', 'hasError', 'errorMessage'])
  })

  it('nodeDataFromTemplate 深拷贝 settings（多节点不共享嵌套引用）', () => {
    const a = nodeDataFromTemplate('http')
    const b = nodeDataFromTemplate('http')
    expect(a?.settings).not.toBe(b?.settings)
    expect(a?.settings?.params).not.toBe(b?.settings?.params)
  })

  it('fromEditorFormat：settings 清洗为引擎 config（方法大写、剔除无名参数/空提取与未知键）', () => {
    const def = fromEditorFormat(httpEditor(), 'x')
    const http = def.nodes.find((n) => n.key === 'http1')!
    expect(http.config).toEqual({
      method: 'POST',
      url: 'https://api.example.com/{start.question}',
      timeoutSeconds: 60,
      params: [{ name: 'q', value: '{start.question}' }],
      headers: [{ name: 'X-Trace', value: 't-1' }],
      bodyType: 'json',
      body: '{"q":"{start.question}"}',
      auth: { type: 'bearer', token: 'tk-1' },
      errorCapture: true,
      extract: [{ name: 'title', path: '$.data.title', fieldType: 'string' }],
    })
  })

  it('toEditorFormat 往返保留 http 节点配置并通过校验', () => {
    const editor2 = toEditorFormat(fromEditorFormat(httpEditor(), 'x'))
    const http = editor2.nodes.find((n) => n.id === 'http1')
    expect(http?.type).toBe('http')
    expect(http?.data?.settings?.method).toBe('POST')
    expect(http?.data?.settings?.auth).toEqual({ type: 'bearer', token: 'tk-1' })
    expect(validateEditorData(editor2)).toEqual([])
  })

  it('validateEditorData：缺请求地址/非上游插值引用/重复提取名/缺 JsonPath', () => {
    const editor = httpEditor()
    const http = editor.nodes.find((n) => n.id === 'http1')!
    const settings = http.data!.settings as Record<string, unknown>

    settings.url = ''
    let errors = validateEditorData(editor)
    expect(errors.some((e) => e.message.includes('未配置请求地址'))).toBe(true)

    settings.url = 'https://api.example.com/{end.answer}'
    errors = validateEditorData(editor)
    expect(errors.some((e) => e.message.includes('非上游节点'))).toBe(true)

    settings.url = 'https://api.example.com/'
    settings.extract = [{ name: 'a', path: '$.x' }, { name: 'a', path: '' }]
    errors = validateEditorData(editor)
    expect(errors.some((e) => e.message.includes('重复的提取字段名'))).toBe(true)
    expect(errors.some((e) => e.message.includes('缺少 JsonPath'))).toBe(true)
  })
})

describe('cURL 导入解析（parseCurlCommand）', () => {
  it('解析 POST JSON + Bearer 头（Bearer 提升为鉴权）', () => {
    const parsed = parseCurlCommand(
      [
        "curl -X POST 'https://api.example.com/search?lang=zh' \\",
        "  -H 'Content-Type: application/json' \\",
        "  -H 'Authorization: Bearer abc123' \\",
        '  -d \'{"query":"天气"}\'',
      ].join('\n'),
    )
    expect(parsed).not.toBeNull()
    expect(parsed!.method).toBe('POST')
    expect(parsed!.url).toBe('https://api.example.com/search?lang=zh')
    expect(parsed!.bodyType).toBe('json')
    expect(parsed!.body).toBe('{"query":"天气"}')
    expect(parsed!.auth).toEqual({ type: 'bearer', token: 'abc123' })
    expect(parsed!.headers).toEqual([{ name: 'Content-Type', value: 'application/json' }])
  })

  it('解析 -u Basic 与多段 -d 表单', () => {
    const parsed = parseCurlCommand("curl 'https://api.example.com/list' -u root:secret -d page=1 -d size=20")
    expect(parsed).not.toBeNull()
    expect(parsed!.method).toBe('POST')
    expect(parsed!.auth).toEqual({ type: 'basic', username: 'root', password: 'secret' })
    expect(parsed!.bodyType).toBe('form')
    expect(parsed!.formEntries).toEqual([
      { name: 'page', value: '1' },
      { name: 'size', value: '20' },
    ])
  })

  it('无 -d 默认 GET；无法提取 URL 时返回 null', () => {
    const parsed = parseCurlCommand("curl 'https://api.example.com/ping'")
    expect(parsed!.method).toBe('GET')
    expect(parsed!.bodyType).toBe('none')
    expect(parseCurlCommand('curl -X GET')).toBeNull()
    expect(parseCurlCommand('')).toBeNull()
  })
})
