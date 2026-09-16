/**
 * 流程设计器主组件：FlowGram 自由布局画布 + 左侧节点面板 + 右侧配置抽屉 + 调试运行面板.
 * 画布 toJSON 是编辑期唯一数据源；保存时转换为引擎 WorkflowDefinition 契约.
 */

import { useCallback, useEffect, useMemo, useState } from 'react'
import { App, Button, Drawer, Space, Spin, Tag, Tooltip, Typography } from 'antd'
import {
  ApartmentOutlined,
  CaretRightOutlined,
  HistoryOutlined,
  SaveOutlined,
  UploadOutlined,
} from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import {
  FreeLayoutEditorProvider,
  EditorRenderer,
  useClientContext,
  useNodeRender,
  usePlaygroundTools,
  WorkflowNodeProps,
  WorkflowNodeRenderer,
  WorkflowNodeRegistry,
} from '@flowgram.ai/free-layout-editor'
import { createMinimapPlugin } from '@flowgram.ai/minimap-plugin'
import { createFreeSnapPlugin } from '@flowgram.ai/free-snap-plugin'
import '@flowgram.ai/free-layout-editor/index.css'

import { FreeLayoutPluginContext } from '@flowgram.ai/free-layout-editor'
import { useWorkflowDesignerStore } from './store'
import { CONDITION_PORTS, getNodeTemplate, NODE_CONSTRAINTS, NODE_TEMPLATES } from './constants'
import { createDefaultEditorData, nodeDataFromTemplate, validateEditorData } from './utils'
import type { EditorWorkflowJSON, NodeRunState } from './types'
import { NodePanel } from './NodePanel'
import { ConfigPanel } from './ConfigPanel'
import { RunPanel } from './RunPanel'
import './workflow-designer.css'

const { Text } = Typography

// ==================== 画布内节点渲染 ====================

function DefaultNodeRenderer(props: WorkflowNodeProps) {
  const { t } = useTranslation()
  const { data } = useNodeRender()
  const nodeRunStates = useWorkflowDesignerStore((s) => s.nodeRunStates)
  const runState: NodeRunState | undefined = nodeRunStates[props.node.id]
  const template = getNodeTemplate(String(props.node.flowNodeType ?? props.node.type))
  const inputs = Object.keys(data?.inputs ?? {})
  const outputs = data?.outputs ?? []

  return (
    <WorkflowNodeRenderer className="wf-node" node={props.node} data-node-id={props.node.id}>
      {runState && (
        <span
          className={`wf-node-run-badge wf-node-run-${runState.state}`}
          title={runState.errorMessage ?? t(`workflowDesigner.runState.${runState.state}`, { defaultValue: runState.state })}
        />
      )}
      <div className="wf-node-header" style={{ background: template?.color }}>
        {props.node.type === 'condition' && <ApartmentOutlined style={{ marginRight: 4 }} />}
        <span className="wf-node-title">{String(data?.title ?? props.node.id)}</span>
      </div>
      {(inputs.length > 0 || outputs.length > 0) && (
        <div className="wf-node-params">
          {inputs.length > 0 && <div className="wf-node-params-line">IN: {inputs.join(', ')}</div>}
          {outputs.length > 0 && (
            <div className="wf-node-params-line">OUT: {outputs.map((o: { name: string }) => o.name).join(', ')}</div>
          )}
        </div>
      )}
    </WorkflowNodeRenderer>
  )
}

// ==================== 画布（拖拽投放 + 缩放工具） ====================

function DesignerCanvas({ onSelectNode }: { onSelectNode: (nodeId: string) => void }) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { playground, document } = useClientContext()
  const tools = usePlaygroundTools()
  const editorJSON = useWorkflowDesignerStore((s) => s.editorJSON)

  const isEmpty = !editorJSON || editorJSON.nodes.length === 0

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault()
    e.dataTransfer.dropEffect = 'copy'
  }

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault()
    try {
      const raw = e.dataTransfer.getData('application/x-moai-node')
      if (!raw) return
      const template = JSON.parse(raw) as { type: string }
      const type = template.type
      const nodeCount = document.getAllNodes().filter((n) => n.flowNodeType === type).length
      const maxCount = NODE_CONSTRAINTS[type as keyof typeof NODE_CONSTRAINTS]?.maxCount ?? -1
      if (maxCount !== -1 && nodeCount >= maxCount) {
        message.warning(t('workflowDesigner.nodeMaxReached', { type }))
        return
      }
      const position = playground.config.getPosFromMouseEvent(e.nativeEvent)
      const id = `${type}_${Date.now().toString(36)}${Math.random().toString(36).slice(2, 8)}`
      // 用 WorkflowDocument.createWorkflowNode 创建：走工作流专属的实体/端口初始化与内容变更事件
      document.createWorkflowNode({
        id,
        type,
        meta: { position },
        data: nodeDataFromTemplate(type),
        blocks: [],
        edges: [],
      })
    } catch {
      message.error(t('workflowDesigner.dropFailed'))
    }
  }

  return (
    <div className="wf-canvas" onDrop={handleDrop} onDragOver={handleDragOver} onClick={(e) => {
      const target = (e.target as HTMLElement).closest('[data-node-id]')
      if (target) onSelectNode(target.getAttribute('data-node-id') as string)
    }}>
      <EditorRenderer />
      {isEmpty && (
        <div className="wf-canvas-empty">
          <div>{t('workflowDesigner.emptyTitle')}</div>
          <div className="wf-canvas-empty-sub">{t('workflowDesigner.emptySub')}</div>
        </div>
      )}
      <div className="wf-canvas-tools">
        <Button size="small" onClick={() => tools.zoomout()}>-</Button>
        <span className="wf-canvas-zoom">{Math.round((tools.zoom ?? 1) * 100)}%</span>
        <Button size="small" onClick={() => tools.zoomin()}>+</Button>
        <Button size="small" onClick={() => tools.fitView(false)}>{t('workflowDesigner.fitView')}</Button>
      </div>
    </div>
  )
}

// ==================== 编辑器装配 ====================

function useNodeRegistries(): WorkflowNodeRegistry[] {
  return useMemo(
    () =>
      NODE_TEMPLATES.map((template) => ({
        type: template.type,
        meta: {
          isStart: template.type === 'start',
          deleteDisable: !NODE_CONSTRAINTS[template.type].deletable,
          copyDisable: !NODE_CONSTRAINTS[template.type].copyable,
          defaultPorts: [
            ...(NODE_CONSTRAINTS[template.type].requiresInput ? [{ type: 'input' as const }] : []),
            ...(template.type === 'condition'
              ? CONDITION_PORTS
              : NODE_CONSTRAINTS[template.type].requiresOutput
                ? [{ type: 'output' as const }]
                : []),
          ],
        },
      })),
    [],
  )
}

export interface WorkflowDesignerProps {
  teamId: number
  appId: string
  appName?: string
  canManage: boolean
}

export function WorkflowDesigner({ teamId, appId, appName, canManage }: WorkflowDesignerProps) {
  const { t } = useTranslation()
  const { message, modal } = App.useApp()
  const navigate = useNavigate()

  const loading = useWorkflowDesignerStore((s) => s.loading)
  const saving = useWorkflowDesignerStore((s) => s.saving)
  const publishing = useWorkflowDesignerStore((s) => s.publishing)
  const running = useWorkflowDesignerStore((s) => s.running)
  const dirty = useWorkflowDesignerStore((s) => s.dirty)
  const version = useWorkflowDesignerStore((s) => s.version)
  const status = useWorkflowDesignerStore((s) => s.status)
  const runResult = useWorkflowDesignerStore((s) => s.runResult)
  const loadSeq = useWorkflowDesignerStore((s) => s.loadSeq)

  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null)
  const [runPanelOpen, setRunPanelOpen] = useState(false)

  useEffect(() => {
    if (!appId || !teamId) return
    useWorkflowDesignerStore.getState().load(appId, teamId).catch(() => {
      // 错误已由全局请求中间件统一提示
    })
    return () => useWorkflowDesignerStore.getState().reset()
  }, [appId, teamId])

  // 未保存离开提醒
  useEffect(() => {
    const handler = (e: BeforeUnloadEvent) => {
      if (useWorkflowDesignerStore.getState().dirty) {
        e.preventDefault()
        e.returnValue = t('workflowDesigner.unsavedWarning')
      }
    }
    window.addEventListener('beforeunload', handler)
    return () => window.removeEventListener('beforeunload', handler)
  }, [t])

  const nodeRegistries = useNodeRegistries()

  const handleContentChange = useCallback((ctx: FreeLayoutPluginContext) => {
    useWorkflowDesignerStore.getState().setEditorJSON(ctx.document.toJSON() as unknown as EditorWorkflowJSON)
  }, [])

  const initialData = useWorkflowDesignerStore((s) => s.initialData)

  const editorProps = useMemo(() => {
    const data = (initialData ?? createDefaultEditorData()) as unknown as NonNullable<
      React.ComponentProps<typeof FreeLayoutEditorProvider>['initialData']
    >
    return {
      initialData: data,
      background: true,
      readonly: !canManage,
      nodeRegistries,
      getNodeDefaultRegistry: (type: string | number) => ({
        type,
        meta: { defaultExpanded: true },
        formMeta: {
          render: () => null,
        },
      }),
      materials: {
        renderDefaultNode: (props: WorkflowNodeProps) => <DefaultNodeRenderer {...props} />,
      },
      nodeEngine: { enable: true },
      history: { enable: true, enableChangeNode: canManage },
      onAllLayersRendered: (ctx: FreeLayoutPluginContext) => {
        void ctx.document.fitView(false)
      },
      plugins: () => [
        createMinimapPlugin({
          disableLayer: true,
          canvasStyle: {
            canvasWidth: 182,
            canvasHeight: 102,
            canvasPadding: 50,
            canvasBackground: 'rgba(245, 245, 245, 1)',
            canvasBorderRadius: 10,
            viewportBackground: 'rgba(235, 235, 235, 1)',
            viewportBorderRadius: 4,
            nodeColor: 'rgba(255, 255, 255, 1)',
            nodeBorderRadius: 2,
          },
        }),
        createFreeSnapPlugin({
          edgeColor: '#00B2B2',
          alignColor: '#00B2B2',
          edgeLineWidth: 1,
          alignLineWidth: 1,
          alignCrossWidth: 8,
        }),
      ],
      onContentChange: handleContentChange,
    }
  }, [initialData, nodeRegistries, canManage, handleContentChange])

  // 保存前客户端校验，错误逐条提示
  const validateBefore = useCallback(
    (json: EditorWorkflowJSON | null, requireFull: boolean): boolean => {
      const errors = validateEditorData(json)
      if (errors.length > 0) {
        message.warning(`${t('workflowDesigner.validateFailed')}: ${errors[0].message}`)
        return false
      }
      void requireFull
      return true
    },
    [message, t],
  )

  const handleSave = async () => {
    const store = useWorkflowDesignerStore.getState()
    if (!store.editorJSON && !store.initialData) return
    if (!validateBefore(store.editorJSON ?? store.initialData, true)) return
    try {
      await store.save()
      message.success(t('workflowDesigner.saveSuccess'))
    } catch {
      // 校验/请求错误已由全局请求中间件统一提示
    }
  }

  const handlePublish = () => {
    const store = useWorkflowDesignerStore.getState()
    const json = store.editorJSON ?? store.initialData
    if (!validateBefore(json, true)) return
    modal.confirm({
      title: t('workflowDesigner.publishConfirm'),
      content: t('workflowDesigner.publishConfirmDesc'),
      maskClosable: false,
      onOk: async () => {
        try {
          // 发布的是当前草稿，先保存
          await store.save()
          await store.publish()
          message.success(t('workflowDesigner.publishSuccess'))
        } catch {
          // 校验/请求错误已由全局请求中间件统一提示
        }
      },
    })
  }

  const handleRun = () => {
    const store = useWorkflowDesignerStore.getState()
    if (!validateBefore(store.editorJSON ?? store.initialData, false)) return
    setSelectedNodeId(null)
    setRunPanelOpen(true)
  }

  if (!appId) return null

  return (
    <div className="wf-designer">
      <div className="wf-header">
        <Space size="small">
          <Text strong>{appName || t('workflowDesigner.title')}</Text>
          {version > 0 && <Tag>{t('workflowDesigner.version', { version })}</Tag>}
          {status === 1 ? <Tag color="green">{t('workflowDesigner.published')}</Tag> : <Tag color="orange">{t('workflowDesigner.draft')}</Tag>}
          {dirty && <Tag color="red">{t('workflowDesigner.unsaved')}</Tag>}
        </Space>
        {canManage && (
          <Space size="small">
            <Tooltip title={t('workflowDesigner.runTip')}>
              <Button icon={<CaretRightOutlined />} loading={running} onClick={handleRun}>
                {t('workflowDesigner.run')}
              </Button>
            </Tooltip>
            <Button icon={<SaveOutlined />} loading={saving} onClick={() => void handleSave()}>
              {t('workflowDesigner.save')}
            </Button>
            <Button type="primary" icon={<UploadOutlined />} loading={publishing} onClick={handlePublish}>
              {t('workflowDesigner.publish')}
            </Button>
            <Tooltip title={t('workflowDesigner.runsTip')}>
              <Button icon={<HistoryOutlined />} onClick={() => navigate(`/team/${teamId}/app/${appId}/runs`)}>
                {t('workflowDesigner.runs')}
              </Button>
            </Tooltip>
          </Space>
        )}
      </div>

      {loading ? (
        <div className="wf-loading">
          <Spin size="large" />
        </div>
      ) : (
        <div className="wf-content">
          <FreeLayoutEditorProvider key={`${appId}-${loadSeq}`} {...editorProps}>
            {canManage && <NodePanel />}
            <DesignerCanvas onSelectNode={setSelectedNodeId} />
            <ConfigPanel
              teamId={teamId}
              nodeId={selectedNodeId}
              onClose={() => setSelectedNodeId(null)}
            />
          </FreeLayoutEditorProvider>
        </div>
      )}

      <Drawer
        title={t('workflowDesigner.runPanelTitle')}
        placement="right"
        width={520}
        open={runPanelOpen}
        onClose={() => setRunPanelOpen(false)}
        maskClosable={false}
        destroyOnClose
      >
        <RunPanel
          running={running}
          result={runResult}
          onRun={(inputJson) =>
            (async () => {
              const store = useWorkflowDesignerStore.getState()
              try {
                // 边改边试：以当前画布保存并执行
                const definition = await store.save()
                await store.run(inputJson, JSON.stringify(definition), store.editorJSON ?? undefined)
              } catch {
                // 校验/请求错误已由全局请求中间件统一提示
              }
            })()
          }
        />
      </Drawer>
    </div>
  )
}
