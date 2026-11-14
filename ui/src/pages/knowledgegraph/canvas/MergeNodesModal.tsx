import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Alert, Button, Modal, Popconfirm, Radio, Space, Typography } from 'antd'
import { feedback } from '@/design-system'
import { mergeKnowledgeGraphNodes } from '@/api/knowledgeGraph'
import type { CanvasNode } from './types'

interface MergeNodesModalProps {
  open: boolean
  graphId: number
  /** 待合并节点（含目标候选），≥2 个 */
  nodes: CanvasNode[]
  typeNameOf: (node: CanvasNode) => string
  onCancel: () => void
  /** 合并完成后回调（父层刷新画布） */
  onMerged: (result: { rehungEdges: number; removedNodes: number }) => void
}

/** 实体合并对话框：选择保留目标 → 展示差异摘要 → 二次确认（不可逆）→ 调用合并接口 */
export function MergeNodesModal({ open, graphId, nodes, typeNameOf, onCancel, onMerged }: MergeNodesModalProps) {
  const { t } = useTranslation()
  const [targetId, setTargetId] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)
  // nodes 变化时默认选度数最高者为目标
  const effectiveTarget = targetId != null && nodes.some((n) => n.id === targetId)
    ? targetId
    : [...nodes].sort((a, b) => (b.degree ?? 0) - (a.degree ?? 0))[0]?.id ?? null

  const submit = async () => {
    if (effectiveTarget == null) return
    const sourceIds = nodes.filter((n) => n.id !== effectiveTarget).map((n) => n.id)
    if (sourceIds.length === 0) return
    setSaving(true)
    try {
      const result = await mergeKnowledgeGraphNodes(graphId, { targetNodeId: effectiveTarget, sourceNodeIds: sourceIds, confirm: true })
      feedback.success(t('knowledgegraph.canvas.mergeSuccess', {
        rehung: result.rehungEdges ?? 0,
        removed: result.removedNodes ?? 0,
      }))
      onMerged({ rehungEdges: result.rehungEdges ?? 0, removedNodes: result.removedNodes ?? 0 })
    } catch {
      // 错误已由全局请求中间件统一提示（如起止类型约束 400）
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal
      open={open}
      title={t('knowledgegraph.canvas.mergeTitle')}
      onCancel={onCancel}
      destroyOnHidden
      maskClosable={false}
      footer={[
        <Button key="cancel" onClick={onCancel}>{t('common.cancel')}</Button>,
        <Popconfirm
          key="ok"
          title={t('knowledgegraph.canvas.mergeConfirmTitle')}
          description={t('knowledgegraph.canvas.mergeConfirmDesc')}
          okButtonProps={{ danger: true }}
          okText={t('knowledgegraph.canvas.mergeConfirmOk')}
          onConfirm={() => void submit()}
        >
          <Button type="primary" danger loading={saving} disabled={effectiveTarget == null || nodes.length < 2}>
            {t('knowledgegraph.canvas.mergeSubmit')}
          </Button>
        </Popconfirm>,
      ]}
    >
      <Alert
        type="warning"
        showIcon
        message={t('knowledgegraph.canvas.mergeHint')}
        style={{ marginBottom: 12 }}
      />
      <Typography.Paragraph type="secondary" style={{ fontSize: 12 }}>
        {t('knowledgegraph.canvas.mergeTargetHint')}
      </Typography.Paragraph>
      <Radio.Group
        value={effectiveTarget}
        onChange={(e) => setTargetId(e.target.value as string)}
        style={{ display: 'flex', flexDirection: 'column', gap: 8 }}
      >
        {nodes.map((node) => (
          <Radio key={node.id} value={node.id}>
            <Space direction="vertical" size={0}>
              <span style={{ fontWeight: 500 }}>
                {node.label}
                {node.degree != null && node.degree > 0 ? `（${t('knowledgegraph.canvas.degreeTag', { count: node.degree })}）` : ''}
              </span>
              <span style={{ fontSize: 12, opacity: 0.7 }}>
                {typeNameOf(node)}
                {node.description ? ` · ${node.description.slice(0, 40)}` : ''}
              </span>
            </Space>
          </Radio>
        ))}
      </Radio.Group>
    </Modal>
  )
}
