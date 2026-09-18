/**
 * 流程设计器主组件：FlowGram 自由布局画布 + 左侧节点面板 + 右侧配置抽屉 + 调试运行面板.
 * 画布 toJSON 是编辑期唯一数据源；保存时转换为引擎 WorkflowDefinition 契约.
 */

import { useCallback, useEffect, useMemo, useState } from 'react'
import { App, Button, Drawer, Input, Space, Spin, Tooltip } from 'antd'
import {
  ArrowLeftOutlined,
  EditOutlined,
  PlusOutlined,
  SettingOutlined,
  UndoOutlined,
  RedoOutlined,
  ExpandOutlined,
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
  WorkflowNodeLinesData,
  EditorState,
  type onDragLineEndParams,
} from '@flowgram.ai/free-layout-editor'
import { createMinimapPlugin } from '@flowgram.ai/minimap-plugin'
import { createFreeSnapPlugin } from '@flowgram.ai/free-snap-plugin'
import '@flowgram.ai/free-layout-editor/index.css'

import { FreeLayoutPluginContext } from '@flowgram.ai/free-layout-editor'
import { useWorkflowDesignerStore } from './store'
import { CONDITION_PORTS, getNodeTemplate, NODE_CONSTRAINTS, NODE_TEMPLATES } from './constants'
import { createDefaultEditorData, nodeDataFromTemplate, validateEditorData } from './utils'
import type { EditorWorkflowJSON, NodeRunState, OutputField } from './types'
import type { FieldBinding } from './types'
import { NodeLibraryPanel } from './NodePanel'
import { SystemSettingsPanel } from './SystemSettingsPanel'
import { RunPanel } from './RunPanel'
import { NodeForm } from './NodeForm'
import './workflow-designer.css'

/** 左侧浮层面板：无 / 节点库 / 系统设置（互斥，画布左上工具列唤出） */
type LeftPanel = 'none' | 'nodes' | 'settings'

// ==================== 画布内节点渲染 ====================

/** 头部可编辑文本（标题/副标题共用）：点击就地编辑，Enter/失焦提交，Esc 取消 */
function EditableNodeText({
  value,
  placeholder,
  readonly,
  className,
  onCommit,
}: {
  value: string
  placeholder: string
  readonly: boolean
  className: string
  onCommit: (v: string) => void
}) {
  const { t } = useTranslation()
  const [editing, setEditing] = useState(false)
  const [draft, setDraft] = useState(value)

  if (editing) {
    return (
      <Input
        size="small"
        autoFocus
        value={draft}
        className={`${className} wf-editable-input`}
        onChange={(e) => setDraft(e.target.value)}
        onBlur={() => {
          setEditing(false)
          onCommit(draft.trim())
        }}
        onKeyDown={(e) => {
          if (e.key === 'Enter') {
            setEditing(false)
            onCommit(draft.trim())
          }
          if (e.key === 'Escape') setEditing(false)
        }}
      />
    )
  }
  return (
    <div
      className={`${className} wf-editable-text${readonly ? ' wf-editable-readonly' : ''}`}
      title={readonly ? undefined : t('workflowDesigner.clickToEdit')}
      onClick={() => {
        if (readonly) return
        setDraft(value)
        setEditing(true)
      }}
    >
      <span className={value ? undefined : 'wf-editable-placeholder'}>{value || placeholder}</span>
      {!readonly && <EditOutlined className="wf-editable-icon" />}
    </div>
  )
}

function DefaultNodeRenderer(props: WorkflowNodeProps) {
  const { t } = useTranslation()
  const { data, updateData, selected, readonly } = useNodeRender()
  const nodeRunStates = useWorkflowDesignerStore((s) => s.nodeRunStates)
  const runState: NodeRunState | undefined = nodeRunStates[props.node.id]
  const nodeFlowType = String(props.node.flowNodeType ?? props.node.type)
  const template = getNodeTemplate(nodeFlowType)
  const nodeId = props.node.id

  return (
    <WorkflowNodeRenderer className={`wf-node ${selected ? 'wf-node-selected' : ''}`} node={props.node} data-node-id={nodeId}>
      {runState && (
        <span
          className={`wf-node-run-badge wf-node-run-${runState.state}`}
          title={runState.errorMessage ?? t(`workflowDesigner.runState.${runState.state}`, { defaultValue: runState.state })}
        />
      )}
      <div className="wf-node-header">
        <span className="wf-node-icon" style={{ background: template?.color }}>
          {template?.icon ?? '◆'}
        </span>
        <div className="wf-node-head-text">
          <EditableNodeText
            className="wf-node-title"
            value={String(data?.title ?? '')}
            placeholder={String(template?.name ?? nodeId)}
            readonly={readonly}
            onCommit={(v) => updateData({ ...data, title: v })}
          />
          <EditableNodeText
            className="wf-node-subtitle"
            value={String(data?.content ?? '')}
            placeholder={t(`workflowDesigner.nodeDesc_${nodeFlowType}`, { defaultValue: template?.desc ?? '' })}
            readonly={readonly}
            onCommit={(v) => updateData({ ...data, content: v })}
          />
        </div>
      </div>
      <div className="wf-node-body">
        <NodeForm nodeId={nodeId} nodeType={nodeFlowType} readonly={readonly} />
      </div>
    </WorkflowNodeRenderer>
  )
}

// ==================== 画布（拖拽投放 + 缩放工具） ====================

/** 画布右键菜单状态：节点/连线二选一（坐标为视口坐标，菜单用 fixed 定位） */
interface CanvasContextMenu {
  x: number
  y: number
  kind: 'node' | 'line'
  id: string
}

/** 抓手图标（平移模式） */
function HandIcon() {
  return (
    <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M18 11V6a2 2 0 0 0-2-2a2 2 0 0 0-2 2" />
      <path d="M14 10V4a2 2 0 0 0-2-2a2 2 0 0 0-2 2v2" />
      <path d="M10 10.5V6a2 2 0 0 0-2-2a2 2 0 0 0-2 2v8" />
      <path d="M18 8a2 2 0 1 1 4 0v6a8 8 0 0 1-8 8h-2c-2.8 0-4.5-.86-5.99-2.34l-3.6-3.6a2 2 0 0 1 2.83-2.82L7 15" />
    </svg>
  )
}

function DesignerCanvas({ canManage }: { canManage: boolean }) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { playground, document, history } = useClientContext()
  const tools = usePlaygroundTools()

  const [ctxMenu, setCtxMenu] = useState<CanvasContextMenu | null>(null)
  /** 抓手（平移）模式：开启后拖拽画布平移，再次点击恢复选择模式 */
  const [handMode, setHandMode] = useState(false)

  const toggleHandMode = () => {
    const stateConfig = playground.editorState
    if (handMode) {
      stateConfig.toDefaultState()
      setHandMode(false)
    } else {
      stateConfig.changeState(EditorState.STATE_MOUSE_FRIENDLY_SELECT.id)
      setHandMode(true)
    }
  }

  // 菜单打开期间：点击任意位置或再次右键即关闭
  useEffect(() => {
    if (!ctxMenu) return
    const close = () => setCtxMenu(null)
    window.addEventListener('mousedown', close)
    window.addEventListener('contextmenu', close)
    return () => {
      window.removeEventListener('mousedown', close)
      window.removeEventListener('contextmenu', close)
    }
  }, [ctxMenu])

  const handleCanvasContextMenu = (e: React.MouseEvent) => {
    if (!canManage) return
    const target = e.target as HTMLElement
    const pos = playground.config.getPosFromMouseEvent(e.nativeEvent)

    // 线条：右键弹出「删除连线」
    if (target.closest('.gedit-flow-activity-line')) {
      const line = document.linesManager.getCloseInLineFromMousePos(pos)
      if (line) {
        e.preventDefault()
        e.stopPropagation()
        setCtxMenu({ x: e.clientX, y: e.clientY, kind: 'line', id: line.id })
        return
      }
    }

    // 节点：右键弹出「删除节点」（FlowGram 的节点包装层自带 data-node-id）
    const nodeEl = target.closest('.gedit-flow-activity-node') as HTMLElement | null
    const nodeId = nodeEl?.getAttribute('data-node-id')
    if (nodeEl && nodeId) {
      e.preventDefault()
      e.stopPropagation()
      setCtxMenu({ x: e.clientX, y: e.clientY, kind: 'node', id: nodeId })
    }
  }

  const removeCtxTarget = () => {
    if (!ctxMenu) return
    if (ctxMenu.kind === 'node') {
      document.getAllNodes().find((n) => n.id === ctxMenu.id)?.dispose()
    } else {
      document.linesManager.getLineById(ctxMenu.id)?.dispose()
    }
    setCtxMenu(null)
  }

  const handleDragOver = (e: React.DragEvent) => {
    e.preventDefault()
    e.dataTransfer.dropEffect = 'copy'
  }

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault()
    try {
      const raw = e.dataTransfer.getData('application/x-moai-node')
      if (!raw) return
      // 普通投放：{ type }；团队工具投放：额外携带 pluginKey/title/description/inputs/outputs（直接生成已绑定插件的插件节点）
      const payload = JSON.parse(raw) as {
        type: string
        pluginKey?: string
        title?: string
        description?: string
        inputs?: Record<string, FieldBinding>
        outputs?: OutputField[]
      }
      const type = payload.type
      const nodeCount = document.getAllNodes().filter((n) => n.flowNodeType === type).length
      const maxCount = NODE_CONSTRAINTS[type as keyof typeof NODE_CONSTRAINTS]?.maxCount ?? -1
      if (maxCount !== -1 && nodeCount >= maxCount) {
        message.warning(t('workflowDesigner.nodeMaxReached', { type }))
        return
      }
      const position = playground.config.getPosFromMouseEvent(e.nativeEvent)
      const id = `${type}_${Date.now().toString(36)}${Math.random().toString(36).slice(2, 8)}`
      const data = nodeDataFromTemplate(type)
      if (data && payload.pluginKey) {
        data.settings = { ...data.settings, pluginKey: payload.pluginKey }
        if (payload.title) data.title = payload.title
        if (payload.description) data.content = payload.description
        if (payload.inputs && Object.keys(payload.inputs).length > 0) data.inputs = payload.inputs
        if (payload.outputs && payload.outputs.length > 0) data.outputs = payload.outputs
      }
      // 用 WorkflowDocument.createWorkflowNode 创建：走工作流专属的实体/端口初始化与内容变更事件
      document.createWorkflowNode({
        id,
        type,
        meta: { position },
        data,
        blocks: [],
        edges: [],
      })
    } catch {
      message.error(t('workflowDesigner.dropFailed'))
    }
  }

  return (
    <div
      className={`wf-canvas${handMode ? ' wf-canvas-hand' : ''}`}
      onDrop={handleDrop}
      onDragOver={handleDragOver}
      onContextMenu={handleCanvasContextMenu}
    >
      <EditorRenderer />
      {ctxMenu && (
        <>
          {/* 透明遮罩：点击任意处关闭菜单（菜单自身 mousedown 阻断冒泡保持打开） */}
          <div className="wf-ctx-backdrop" onMouseDown={(e) => { e.stopPropagation(); setCtxMenu(null) }} onContextMenu={(e) => { e.preventDefault(); e.stopPropagation(); setCtxMenu(null) }} />
          <div className="wf-ctx-menu" style={{ left: ctxMenu.x, top: ctxMenu.y }} onMouseDown={(e) => e.stopPropagation()}>
            <div
              className="wf-ctx-item wf-ctx-item-danger"
              onClick={() => {
                removeCtxTarget()
                message.success(ctxMenu.kind === 'node' ? t('workflowDesigner.ctxNodeDeleted') : t('workflowDesigner.ctxLineDeleted'))
              }}
            >
              {ctxMenu.kind === 'node' ? t('workflowDesigner.ctxDeleteNode') : t('workflowDesigner.ctxDeleteLine')}
            </div>
          </div>
        </>
      )}
      <div className="wf-canvas-tools">
        <Tooltip title={t('workflowDesigner.handTool')}>
          <Button
            size="small"
            type="text"
            icon={<HandIcon />}
            className={handMode ? 'wf-tools-btn-active' : ''}
            onClick={toggleHandMode}
          />
        </Tooltip>
        <Tooltip title={t('workflowDesigner.undo')}>
          <Button size="small" type="text" icon={<UndoOutlined />} onClick={() => history.undo()} />
        </Tooltip>
        <Tooltip title={t('workflowDesigner.redo')}>
          <Button size="small" type="text" icon={<RedoOutlined />} onClick={() => history.redo()} />
        </Tooltip>
        <span className="wf-tools-divider" />
        <Button size="small" type="text" onClick={() => tools.zoomout()}>-</Button>
        <span className="wf-canvas-zoom">{Math.round((tools.zoom ?? 1) * 100)}%</span>
        <Button size="small" type="text" onClick={() => tools.zoomin()}>+</Button>
        <span className="wf-tools-divider" />
        <Tooltip title={t('workflowDesigner.fitView')}>
          <Button size="small" type="text" icon={<ExpandOutlined />} onClick={() => tools.fitView(false)} />
        </Tooltip>
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
          // switch/questionClassifier 的出边端口按节点表单内容动态计算（分支/分类锚点）
          ...(template.type === 'switch' || template.type === 'questionClassifier' ? { useDynamicPort: true } : {}),
          defaultPorts: [
            ...(NODE_CONSTRAINTS[template.type].requiresInput ? [{ type: 'input' as const }] : []),
            ...(template.type === 'condition'
              ? CONDITION_PORTS
              : NODE_CONSTRAINTS[template.type].requiresOutput && template.type !== 'switch' && template.type !== 'questionClassifier'
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

  const [leftPanel, setLeftPanel] = useState<LeftPanel>('none')
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
      // 普通节点（非条件/多条件/问题分类）拖出新连线时替换旧连线：拖线即切换下游节点
      onDragLineEnd: (_ctx: FreeLayoutPluginContext, params: onDragLineEndParams) => {
        const { fromPort, line } = params
        if (!fromPort || !line) return Promise.resolve()
        const node = fromPort.node
        const type = String(node.flowNodeType ?? node.type)
        if (type === 'condition' || type === 'switch' || type === 'questionClassifier' || type === 'end') return Promise.resolve()
        for (const old of node.getData(WorkflowNodeLinesData)?.outputLines ?? []) {
          if (old.id !== line.id) old.dispose()
        }
        return Promise.resolve()
      },
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
    setRunPanelOpen(true)
  }

  if (!appId) return null

  return (
    <div className="wf-designer">
      <div className="wf-header">
        <div className="wf-header-left">
          <Tooltip title={t('appManage.backToList')}>
            <Button type="text" icon={<ArrowLeftOutlined />} onClick={() => navigate(`/team/${teamId}/apps`)} />
          </Tooltip>
          <div className="wf-header-titles">
            <div className="wf-header-name">{appName || t('workflowDesigner.title')}</div>
            <div className="wf-header-status">
              <span className={`wf-status-dot ${status === 1 ? 'wf-status-dot-published' : 'wf-status-dot-draft'}`} />
              {status === 1 ? t('workflowDesigner.published') : t('workflowDesigner.draft')}
              {version > 0 && <span>· v{version}</span>}
              {dirty && (
                <span className="wf-header-dirty">
                  <span className="wf-status-dot wf-status-dot-dirty" />
                  {t('workflowDesigner.unsaved')}
                </span>
              )}
            </div>
          </div>
        </div>
        {canManage && (
          <Space size="small">
            <Tooltip title={t('workflowDesigner.runsTip')}>
              <Button icon={<HistoryOutlined />} onClick={() => navigate(`/team/${teamId}/app/${appId}/runs`)} />
            </Tooltip>
            <span className="wf-tools-divider" />
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
            <DesignerCanvas canManage={canManage} />
          </FreeLayoutEditorProvider>
          {canManage && leftPanel !== 'none' && (
            <div className="wf-side-layer">
              {leftPanel === 'nodes' ? <NodeLibraryPanel onClose={() => setLeftPanel('none')} /> : <SystemSettingsPanel onClose={() => setLeftPanel('none')} />}
            </div>
          )}
          {canManage && leftPanel === 'none' && (
            <div className="wf-left-toolbar">
              <Tooltip title={t('workflowDesigner.addNode')} placement="right">
                <Button
                  className="wf-left-tool-add"
                  shape="circle"
                  icon={<PlusOutlined />}
                  onClick={() => setLeftPanel('nodes')}
                />
              </Tooltip>
              <Tooltip title={t('workflowDesigner.systemSettings')} placement="right">
                <Button shape="circle" icon={<SettingOutlined />} onClick={() => setLeftPanel('settings')} />
              </Tooltip>
            </div>
          )}
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
          onRun={(inputJson, systemJson) =>
            (async () => {
              const store = useWorkflowDesignerStore.getState()
              try {
                // 边改边试：以当前画布保存并执行
                const definition = await store.save()
                await store.run(inputJson, JSON.stringify(definition), store.editorJSON ?? undefined, systemJson)
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
