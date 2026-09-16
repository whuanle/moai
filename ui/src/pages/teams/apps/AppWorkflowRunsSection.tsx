/**
 * 流程应用运行历史分区：分页展示调试/正式运行实例，可查看节点级执行详情.
 */

import { useCallback, useEffect, useState } from 'react'
import { Button, Drawer, Space, Table, Tag, Typography } from 'antd'
import { ReloadOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { DataTable } from '@/design-system'
import { getWorkflowInstance, getWorkflowInstances, type WorkflowInstanceDetail, type WorkflowInstanceItem } from '@/api/workflow'
import { formatDateTime } from '@/utils/datetime'

const STATUS_COLOR: Record<number, string> = {
  0: 'default',
  1: 'processing',
  2: 'warning',
  3: 'success',
  4: 'default',
}

export function AppWorkflowRunsSection({ teamId, appId, canManage }: { teamId: number; appId: string; canManage: boolean }) {
  const { t } = useTranslation()

  const [items, setItems] = useState<WorkflowInstanceItem[]>([])
  const [total, setTotal] = useState(0)
  const [pageNo, setPageNo] = useState(1)
  const [pageSize, setPageSize] = useState(10)
  const [loading, setLoading] = useState(false)
  const [detail, setDetail] = useState<WorkflowInstanceDetail | null>(null)
  const [detailLoading, setDetailLoading] = useState(false)

  const load = useCallback(async () => {
    if (!appId || !canManage) return
    setLoading(true)
    try {
      const res = await getWorkflowInstances(appId, teamId, { pageNo, pageSize })
      setItems(res.items ?? [])
      setTotal(res.total ?? 0)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [appId, teamId, pageNo, pageSize, canManage])

  useEffect(() => {
    void load()
  }, [load])

  const openDetail = async (instanceId: string) => {
    setDetailLoading(true)
    try {
      setDetail(await getWorkflowInstance(appId, teamId, instanceId))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setDetailLoading(false)
    }
  }

  if (!canManage) {
    return <div style={{ padding: 24, textAlign: 'center' }}>{t('appWorkspace.workflowRunsForbidden')}</div>
  }

  return (
    <DataTable
      rowKey="instanceId"
      loading={loading}
      dataSource={items}
      toolbar={
        <Space>
          <Typography.Text strong>{t('appWorkspace.workflowRunsTitle')}</Typography.Text>
          <Button size="small" icon={<ReloadOutlined />} onClick={() => void load()}>
            {t('appWorkspace.workflowRefresh')}
          </Button>
        </Space>
      }
      onRefresh={() => void load()}
      pagination={{
        current: pageNo,
        pageSize,
        total,
        showSizeChanger: true,
        onChange: (page, size) => {
          setPageNo(page)
          setPageSize(size)
        },
      }}
      columns={[
        {
          title: t('appWorkspace.workflowRunId'),
          dataIndex: 'instanceId',
          width: 140,
          ellipsis: true,
        },
        {
          title: t('appWorkspace.workflowRunStatus'),
          dataIndex: 'status',
          width: 100,
          render: (v: number) => (
            <Tag color={STATUS_COLOR[v] ?? 'default'}>{t(`appWorkspace.workflowStatus.${v}`)}</Tag>
          ),
        },
        {
          title: t('appWorkspace.workflowRunKind'),
          dataIndex: 'isDebug',
          width: 90,
          render: (v: boolean) => (v ? <Tag>{t('appWorkspace.workflowRunDebug')}</Tag> : <Tag color="blue">{t('appWorkspace.workflowRunFormal')}</Tag>),
        },
        { title: t('appWorkspace.workflowRunVersion'), dataIndex: 'version', width: 80 },
        {
          title: t('appWorkspace.workflowRunError'),
          dataIndex: 'errorMessage',
          ellipsis: true,
          render: (v: string | null) => v ?? '-',
        },
        {
          title: t('appWorkspace.workflowRunStart'),
          dataIndex: 'startTime',
          width: 170,
          render: (v: string | null) => (v ? formatDateTime(v) : '-'),
        },
        {
          title: t('appWorkspace.workflowRunCreator'),
          dataIndex: 'createUserName',
          width: 110,
          render: (v: string) => v || '-',
        },
        {
          title: t('appWorkspace.workflowRunActions'),
          key: 'actions',
          width: 90,
          render: (_, record: WorkflowInstanceItem) => (
            <Button type="link" size="small" onClick={() => void openDetail(record.instanceId as string)}>
              {t('appWorkspace.workflowRunDetail')}
            </Button>
          ),
        },
      ]}
    >
      <Drawer
        title={t('appWorkspace.workflowRunDetailTitle')}
        placement="right"
        width={640}
        open={detail !== null || detailLoading}
        onClose={() => setDetail(null)}
        maskClosable={false}
      >
        {detail && (
          <div>
            <p>
              <Tag color={STATUS_COLOR[detail.status ?? 0] ?? 'default'}>{t(`appWorkspace.workflowStatus.${detail.status}`)}</Tag>
              {detail.errorMessage && <span style={{ color: '#ff4d4f' }}>{detail.errorMessage}</span>}
            </p>
            <Table
              size="small"
              rowKey="nodeKey"
              pagination={false}
              dataSource={detail.nodes ?? []}
              columns={[
                { title: t('workflowDesigner.colNode'), dataIndex: 'nodeName', width: 110 },
                { title: t('workflowDesigner.colState'), dataIndex: 'state', width: 90 },
                {
                  title: 'IN',
                  dataIndex: 'input',
                  ellipsis: true,
                  render: (v: string | null) => v ?? '-',
                },
                {
                  title: 'OUT',
                  dataIndex: 'output',
                  ellipsis: true,
                  render: (v: string | null) => v ?? '-',
                },
              ]}
            />
          </div>
        )}
      </Drawer>
    </DataTable>
  )
}
