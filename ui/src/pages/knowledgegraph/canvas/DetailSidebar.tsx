import { useTranslation } from 'react-i18next'
import { Button, Descriptions, Drawer, Empty, Popconfirm, Space, Tag, Typography } from 'antd'
import { DeleteOutlined, EditOutlined, ShrinkOutlined, ExpandOutlined } from '@ant-design/icons'
import type { CanvasEdge, CanvasNode } from './types'

/** 关联边行（对端信息已解析） */
export interface RelatedEdgeItem {
  edge: CanvasEdge
  /** 关系名（已按接入图/托管图解析） */
  relationLabel: string
  /** 对端节点名 */
  otherLabel: string
  /** out=出边（当前节点为起点）/ in=入边 */
  direction: 'out' | 'in'
  otherNodeId: string
}

interface DetailSidebarProps {
  node: CanvasNode | null
  edge: CanvasEdge | null
  typeNameOf: (node: CanvasNode) => string
  edgeLabelOf: (edge: CanvasEdge) => string
  /** 模型属性定义顺序（托管图节点详情按定义排序） */
  orderedProps: [string, string][]
  relatedEdges: RelatedEdgeItem[]
  /** 已展开的节点 id 集合（展开态切换提示） */
  expanded: boolean
  canEdit: boolean
  /** 编辑实体：跳维护页实例步骤按名称定位（B8 补 P1 侧栏条目） */
  onEditNode: () => void
  onNeighborClick: (nodeId: string) => void
  onEdgeClick: (edgeId: string) => void
  onToggleExpand: () => void
  onDeleteNode: () => void
  onDeleteEdge: () => void
  onClose: () => void
}

/** 详情侧栏：点节点显示属性/关联边/邻居跳转与展开收起、治理入口；点边显示关系详情 */
export function DetailSidebar(props: DetailSidebarProps) {
  const { t } = useTranslation()
  const { node, edge } = props

  const title = node ? node.label : edge ? props.edgeLabelOf(edge) || edge.id : ''

  return (
    <Drawer open={node != null || edge != null} onClose={props.onClose} title={title} width={380} mask={false}>
      {node ? (
        <>
          <Space wrap style={{ marginBottom: 8 }}>
            {props.expanded && <Tag color="blue">{t('knowledgegraph.canvas.expandedTag')}</Tag>}
            {node.degree != null && node.degree > 0 && <Tag>{t('knowledgegraph.canvas.degreeTag', { count: node.degree })}</Tag>}
          </Space>
          <Descriptions column={1} size="small">
            <Descriptions.Item label={t('knowledgegraph.entity.colType')}>{props.typeNameOf(node) || '-'}</Descriptions.Item>
            <Descriptions.Item label={t('knowledgegraph.entity.colDesc')}>{node.description || '-'}</Descriptions.Item>
          </Descriptions>
          <Typography.Title level={5} style={{ marginTop: 12, marginBottom: 4, fontSize: 13 }}>
            {t('knowledgegraph.detail.propsTitle')}
          </Typography.Title>
          {props.orderedProps.length > 0 ? (
            <Descriptions column={1} size="small">
              {props.orderedProps.map(([key, value]) => (
                <Descriptions.Item key={key} label={key}>{value}</Descriptions.Item>
              ))}
            </Descriptions>
          ) : (
            <div style={{ opacity: 0.65 }}>{t('knowledgegraph.detail.noProps')}</div>
          )}

          <Typography.Title level={5} style={{ marginTop: 12, marginBottom: 4, fontSize: 13 }}>
            {t('knowledgegraph.canvas.relatedEdges', { count: props.relatedEdges.length })}
          </Typography.Title>
          {props.relatedEdges.length > 0 ? (
            <Space direction="vertical" size={2} style={{ width: '100%' }}>
              {props.relatedEdges.map((item) => (
                <Button
                  key={item.edge.id}
                  type="link"
                  size="small"
                  style={{ textAlign: 'left', padding: 0, height: 24, whiteSpace: 'normal' }}
                  onClick={() => props.onNeighborClick(item.otherNodeId)}
                >
                  {item.direction === 'out' ? '→' : '←'} {item.relationLabel} · {item.otherLabel}
                </Button>
              ))}
            </Space>
          ) : (
            <div style={{ opacity: 0.65 }}>{t('knowledgegraph.canvas.noRelatedEdges')}</div>
          )}

          <Typography.Title level={5} style={{ marginTop: 12, marginBottom: 4, fontSize: 13 }}>
            {t('knowledgegraph.canvas.actions')}
          </Typography.Title>
          <Space wrap>
            <Button size="small" icon={props.expanded ? <ShrinkOutlined /> : <ExpandOutlined />} onClick={props.onToggleExpand}>
              {t(props.expanded ? 'knowledgegraph.canvas.collapse' : 'knowledgegraph.canvas.expand')}
            </Button>
            {props.canEdit && (
              <Button size="small" icon={<EditOutlined />} onClick={props.onEditNode}>
                {t('knowledgegraph.canvas.editNodeAction')}
              </Button>
            )}
            {props.canEdit && (
              <Popconfirm
                title={t('knowledgegraph.canvasEdit.deleteNodeTitle')}
                okButtonProps={{ danger: true }}
                onConfirm={props.onDeleteNode}
              >
                <Button size="small" danger icon={<DeleteOutlined />}>
                  {t('knowledgegraph.canvas.deleteNodeAction')}
                </Button>
              </Popconfirm>
            )}
          </Space>
          <Typography.Paragraph type="secondary" style={{ marginTop: 10, marginBottom: 0, fontSize: 12 }}>
            {t('knowledgegraph.canvas.doubleClickHint')}
          </Typography.Paragraph>
        </>
      ) : edge ? (
        <>
          <Descriptions column={1} size="small">
            <Descriptions.Item label={t('knowledgegraph.relation.colType')}>{props.edgeLabelOf(edge) || '-'}</Descriptions.Item>
          </Descriptions>
          {props.canEdit && (
            <Popconfirm
              title={t('knowledgegraph.canvasEdit.deleteEdgeTitle')}
              okButtonProps={{ danger: true }}
              onConfirm={props.onDeleteEdge}
            >
              <Button size="small" danger icon={<DeleteOutlined />}>
                {t('knowledgegraph.canvas.deleteEdgeAction')}
              </Button>
            </Popconfirm>
          )}
        </>
      ) : (
        <Empty />
      )}
    </Drawer>
  )
}
