/**
 * 流程设计器状态 —— FlowGram 画布是编辑期唯一数据源，
 * store 只承载加载/保存/发布/调试执行与运行状态回显.
 */

import { create } from 'zustand'
import { getAppAgentConfig, saveAppAgentConfig } from '@/api/app'
import {
  debugRunWorkflow,
  getWorkflowConfig,
  publishWorkflow,
  saveWorkflowDraft,
  type WorkflowDebugRunResult,
} from '@/api/workflow'
import type { EditorWorkflowJSON, GlobalVariableDef, NodeRunState, WorkflowDefinition } from './types'
import { fromEditorFormat, normalizeEditorData, normalizeReferences, toEditorFormat } from './utils'

export interface WorkflowDesignerState {
  appId: string
  teamId: number
  /** 画布初始数据（加载完成后由编辑器消费，loadSeq 变化触发重建） */
  initialData: EditorWorkflowJSON | null
  /** 最近一次画布 toJSON 产物（保存/调试/校验的数据源） */
  editorJSON: EditorWorkflowJSON | null
  /** 加载序号，变化时编辑器以新 initialData 重建 */
  loadSeq: number

  loading: boolean
  saving: boolean
  publishing: boolean
  running: boolean
  dirty: boolean

  /** 已发布版本号，0=从未发布 */
  version: number
  /** 0=草稿有未发布变更 1=当前草稿已发布 */
  status: number
  publishTime: string | null

  runResult: WorkflowDebugRunResult | null
  /** 画布节点运行状态着色（key 为节点 id） */
  nodeRunStates: Record<string, NodeRunState>

  /** 流程全局变量（随草稿保存；节点经 system.变量名 引用，启动时可赋值） */
  variables: GlobalVariableDef[]
  setVariables: (variables: GlobalVariableDef[]) => void

  /** 对话开场白（存于应用 agent 配置，仅流程应用只写开场白字段；随草稿一并保存） */
  openingStatement: string
  openingStatementEnabled: boolean
  setOpeningStatement: (statement: string, enabled: boolean) => void

  load: (appId: string, teamId: number) => Promise<void>
  setEditorJSON: (json: EditorWorkflowJSON) => void
  /** 保存草稿，返回随保存生成的引擎定义（调试执行可直接复用） */
  save: () => Promise<WorkflowDefinition>
  publish: () => Promise<void>
  run: (inputJson: string, definition?: string, editorJson?: EditorWorkflowJSON, systemJson?: string) => Promise<void>
  clearRun: () => void
  reset: () => void
}

const INITIAL = {
  appId: '',
  teamId: 0,
  initialData: null as EditorWorkflowJSON | null,
  editorJSON: null as EditorWorkflowJSON | null,
  loadSeq: 0,
  loading: false,
  saving: false,
  publishing: false,
  running: false,
  dirty: false,
  version: 0,
  status: 0,
  publishTime: null as string | null,
  runResult: null as WorkflowDebugRunResult | null,
  nodeRunStates: {} as Record<string, NodeRunState>,
  variables: [] as GlobalVariableDef[],
  openingStatement: '',
  openingStatementEnabled: false,
}

export const useWorkflowDesignerStore = create<WorkflowDesignerState>((set, get) => ({
  ...INITIAL,

  load: async (appId, teamId) => {
    set({ ...INITIAL, appId, teamId, loading: true })
    try {
      // 开场白存于应用 agent 配置，查询失败不阻塞设计器加载
      const [config, agentConfig] = await Promise.all([
        getWorkflowConfig(appId, teamId),
        getAppAgentConfig(appId).catch(() => null),
      ])
      const initialData = config.draftEditorData
        ? normalizeReferences(normalizeEditorData(JSON.parse(config.draftEditorData) as EditorWorkflowJSON))
        : toEditorFormat(null)
      let variables: GlobalVariableDef[] = []
      if (config.draftDefinition) {
        try {
          variables = (JSON.parse(config.draftDefinition) as WorkflowDefinition).variables ?? []
        } catch {
          variables = []
        }
      }
      set({
        initialData,
        variables,
        openingStatement: agentConfig?.openingStatement ?? '',
        openingStatementEnabled: agentConfig?.openingStatementEnabled ?? false,
        version: config.version ?? 0,
        status: config.status ?? 0,
        publishTime: config.publishTime ?? null,
        loading: false,
        loadSeq: get().loadSeq + 1,
      })
    } catch (error) {
      ;(window as unknown as Record<string, unknown>).__wfLoadErr = String(
        (error as Error)?.message ?? error,
      )
      set({ loading: false })
      throw error
    }
  },

  setEditorJSON: (json) => {
    set({ editorJSON: json, dirty: true })
  },

  save: async () => {
    const { appId, teamId, editorJSON, initialData, openingStatement, openingStatementEnabled } = get()
    const json = editorJSON ?? initialData
    if (!json) throw new Error('画布尚未初始化')
    set({ saving: true })
    try {
      const definition = fromEditorFormat(json, 'workflow')
      definition.variables = get().variables
      await saveWorkflowDraft(appId, teamId, {
        definition: JSON.stringify(definition),
        editorData: JSON.stringify(json),
      })
      // 开场白随保存一并落库（后端对流程应用只写开场白字段）；草稿已保存，此步失败则整体报错由用户重试
      await saveAppAgentConfig(appId, {
        prompt: '',
        wikiIds: [],
        plugins: [],
        openingStatement,
        openingStatementEnabled,
      })
      set({ saving: false, dirty: false })
      return definition
    } catch (error) {
      set({ saving: false })
      throw error
    }
  },

  publish: async () => {
    const { appId, teamId } = get()
    set({ publishing: true })
    try {
      await publishWorkflow(appId, teamId)
      set({ publishing: false, status: 1, dirty: false })
    } catch (error) {
      set({ publishing: false })
      throw error
    }
  },

  run: async (inputJson, definition, editorJson, systemJson) => {
    const { appId, teamId } = get()
    set({ running: true, runResult: null, nodeRunStates: {} })
    try {
      const result = await debugRunWorkflow(appId, teamId, {
        inputJson,
        systemJson,
        definition,
        editorData: editorJson ? JSON.stringify(editorJson) : undefined,
      })
      const nodeRunStates: Record<string, NodeRunState> = {}
      for (const node of result.nodes ?? []) {
        nodeRunStates[node.nodeKey ?? ''] = {
          state: node.state ?? 'pending',
          errorMessage: node.errorMessage,
          startedAt: node.startedAt,
          endedAt: node.endedAt,
        }
      }
      set({ running: false, runResult: result, nodeRunStates })
    } catch (error) {
      set({ running: false })
      throw error
    }
  },

  setVariables: (variables) => set({ variables, dirty: true }),

  setOpeningStatement: (statement, enabled) =>
    set({ openingStatement: statement, openingStatementEnabled: enabled, dirty: true }),

  clearRun: () => set({ runResult: null, nodeRunStates: {} }),

  reset: () => set({ ...INITIAL }),
}))

// 开发环境把 store 挂到 window 便于排查（生产构建静态消除）
if (import.meta.env.DEV) {
  ;(window as unknown as Record<string, unknown>).__wfStore = useWorkflowDesignerStore
}
