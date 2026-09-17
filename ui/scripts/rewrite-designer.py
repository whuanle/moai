# -*- coding: utf-8 -*-
"""重构 WorkflowDesigner 为 FastGPT 风格（节点内编辑/底部工具栏/去右侧面板）。"""
import io
import sys

P = 'ui/src/pages/teams/apps/workflow/WorkflowDesigner.tsx'
s = io.open(P, encoding='utf-8').read()

# ===== 1. DefaultNodeRenderer → FastGPT 风格宽卡片（节点内表单） =====
start_marker = 'function DefaultNodeRenderer(props: WorkflowNodeProps) {'
end_marker = '// ==================== 画布（拖拽投放 + 缩放工具） ===================='
i1 = s.index(start_marker)
i2 = s.index(end_marker)
new_renderer = u'''function DefaultNodeRenderer(props: WorkflowNodeProps) {
  const { t } = useTranslation()
  const { data, selected, readonly } = useNodeRender()
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
          {template?.icon ?? '\u25c6'}
        </span>
        <div className="wf-node-head-text">
          <div className="wf-node-title">{String(data?.title ?? template?.name ?? nodeId)}</div>
          <div className="wf-node-subtitle">{t(`workflowDesigner.nodeDesc_${nodeFlowType}`, { defaultValue: template?.desc ?? '' })}</div>
        </div>
      </div>
      <div className="wf-node-body">
        <NodeForm nodeId={nodeId} nodeType={nodeFlowType} readonly={readonly} />
      </div>
    </WorkflowNodeRenderer>
  )
}

'''
s = s[:i1] + new_renderer + s[i2:]

# ===== 2. DesignerCanvas：去选中逻辑，工具栏加撤销/重做 =====
start_marker = 'function DesignerCanvas({ onSelectNode }: { onSelectNode: (nodeId: string) => void }) {'
end_marker = '// ==================== 编辑器装配 ===================='
i1 = s.index(start_marker)
i2 = s.index(end_marker)
new_canvas = u'''function DesignerCanvas() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const { playground, document, history } = useClientContext()
  const tools = usePlaygroundTools()

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
    <div className="wf-canvas" onDrop={handleDrop} onDragOver={handleDragOver}>
      <EditorRenderer />
      <div className="wf-canvas-tools">
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

'''
s = s[:i1] + new_canvas + s[i2:]

# ===== 3. imports 更新 =====
s = s.replace(
    u"import { ApartmentOutlined,\n  CaretRightOutlined,\n  HistoryOutlined,\n  SaveOutlined,\n  UploadOutlined,\n} from '@ant-design/icons'",
    u"import { ApartmentOutlined,\n  CaretRightOutlined,\n  ExpandOutlined,\n  HistoryOutlined,\n  RedoOutlined,\n  SaveOutlined,\n  UndoOutlined,\n  UploadOutlined,\n} from '@ant-design/icons'")

s = s.replace(
    u"import { RunPanel } from './RunPanel'\nimport { VariablesDrawerBody } from './VariablesDrawer'\nimport './workflow-designer.css'",
    u"import { RunPanel } from './RunPanel'\nimport { VariablesDrawerBody } from './VariablesDrawer'\nimport { NodeForm } from './NodeForm'\nimport './workflow-designer.css'")

# ===== 4. 主组件调整 =====
s = s.replace(
    u"  const [selectedNodeId, setSelectedNodeId] = useState<string | null>(null)\n  const [runPanelOpen, setRunPanelOpen] = useState(false)",
    u"  const [runPanelOpen, setRunPanelOpen] = useState(false)")
s = s.replace(
    u"    if (!validateBefore(store.editorJSON ?? store.initialData, false)) return\n    setSelectedNodeId(null)\n    setRunPanelOpen(true)",
    u"    if (!validateBefore(store.editorJSON ?? store.initialData, false)) return\n    setRunPanelOpen(true)")
s = s.replace(
    u"            {canManage && <NodePanel />}\n            <DesignerCanvas onSelectNode={setSelectedNodeId} />\n            <ConfigPanel\n              teamId={teamId}\n              nodeId={selectedNodeId}\n              onClose={() => setSelectedNodeId(null)}\n            />\n          </FreeLayoutEditorProvider>",
    u"            {canManage && <NodePanel />}\n            <DesignerCanvas />\n          </FreeLayoutEditorProvider>")

io.open(P, 'w', encoding='utf-8').write(s)
print('designer rewritten ok')
