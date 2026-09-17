import { describe, expect, it } from 'vitest'
import {
  collectEdges,
  collectUpstreamVariables,
  normalizeEditorData,
  normalizeReferences,
  createDefaultEditorData,
  fromEditorFormat,
  nodeDataFromTemplate,
  toEditorFormat,
  validateEditorData,
} from '../utils'
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
        outputs: [{ name: 'query', fieldType: 'string', isRequired: true }],
      },
      {
        key: 'search',
        name: '检索',
        type: 'plugin',
        config: { pluginKey: 'mock.knowledgeSearch' },
        inputs: { query: { expressionType: 'variable', value: 'start.query', required: true } },
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
            value: '问题：{start.query}',
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
        inputs: { query: { expressionType: 'variable', value: 'start.query' } },
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

  it('collectUpstreamVariables 提供 sys 与祖先节点输出', () => {
    const editor = toEditorFormat(buildDefinition())
    const options = collectUpstreamVariables(editor, 'check')
    const values = options.map((o) => o.value)
    expect(values).toContain('sys.instanceId')
    expect(values).toContain('start.query')
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
    const decl = start.data!.inputs!.query!
    expect(decl.expressionType).toBe('run')
    expect(decl.required).toBe(true)
    expect(decl.fieldType).toBe('string')

    // fromEditorFormat：声明映射回 outputs 供引擎校验，definition.inputs 置空
    const restored = fromEditorFormat(editor, 'x')
    const startDef = restored.nodes.find((n) => n.key === 'start')!
    expect(startDef.inputs).toEqual({})
    expect(startDef.outputs).toHaveLength(1)
    expect(startDef.outputs[0]).toMatchObject({ name: 'query', fieldType: 'string', isRequired: true })
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
})

describe('节点 Key（data.key 覆盖）', () => {
  /** 带自定义 key 的画布：js_1 改名为 lookup、cond_1 改名为 check，引用仍写旧 id（改 key 未重载的场景） */
  const keyEditor = (): import('../types').EditorWorkflowJSON => ({
    nodes: [
      { id: 'start', type: 'start', data: { title: '开始', inputs: { query: { expressionType: 'run', value: '', required: true, fieldType: 'string' } } }, blocks: [], edges: [{ sourceNodeID: 'start', targetNodeID: 'js_1' }] },
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
    expect(options).toContain('start.query')
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
        { id: 'b1', label: '条件 1', binding: { expressionType: 'variable', value: 'start.flag', required: true } },
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
        { id: 'b1', label: '条件 1', binding: { expressionType: 'variable', value: 'start.flag', required: true } },
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
