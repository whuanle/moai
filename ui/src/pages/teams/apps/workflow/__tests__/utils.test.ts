import { describe, expect, it } from 'vitest'
import {
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

  it('定义 → 编辑器：条件节点出边带 true/false 端口', () => {
    const editor = toEditorFormat(buildDefinition())
    const check = editor.nodes.find((n) => n.id === 'check')
    expect(check).toBeDefined()
    const ports = (check?.edges ?? []).map((e) => String(e.sourcePortID)).sort()
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
})

describe('workflow validation', () => {
  it('缺少开始节点报错', () => {
    const editor = toEditorFormat(buildDefinition())
    editor.nodes = editor.nodes.filter((n) => n.type !== 'start')
    // 连带清理指向 start 的边
    for (const node of editor.nodes) {
      node.edges = (node.edges ?? []).filter((e) => e.sourceNodeID !== 'start')
    }
    const errors = validateEditorData(editor)
    expect(errors.some((e) => e.message.includes('开始节点'))).toBe(true)
  })

  it('条件节点缺少 false 出边报错', () => {
    const editor = toEditorFormat(buildDefinition())
    const check = editor.nodes.find((n) => n.id === 'check')
    check!.edges = (check!.edges ?? []).filter((e) => String(e.sourcePortID) !== 'false')
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
