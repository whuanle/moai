import { useCallback, useEffect, useMemo, useState } from 'react'
import { CheckOutlined, CloseOutlined, ReloadOutlined } from '@ant-design/icons'
import { Avatar, Button, Input, Modal, Popconfirm, Select, Space, Tag, Tooltip, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { Navigate } from 'react-router'
import { AuditCell, DataTable, Page, PageToolbar, feedback } from '@/design-system'
import { controlHeight, spacing, tableCardChromeHeight } from '@/design-system/theme'
import { useAppStore } from '@/store/app'
import { formatAppVersion } from '@/utils/appVersion'
import { resolveStorageUrl } from '@/utils/storage'
import {
  getPublicationList,
  reviewPublication,
  type PublicationResourceType,
  type PublicationReviewItem,
  type PublicationState,
} from '@/api/publication'

const { Text } = Typography

type StateFilter = PublicationState | 'all'
type ResourceTypeFilter = PublicationResourceType | 'all'

const STATE_TAG_COLOR: Record<PublicationState, string> = {
  pending: 'orange',
  approved: 'green',
  rejected: 'error',
}

const pageVerticalPadding = spacing.lg * 2
const toolbarHeight = controlHeight + spacing.md
const tableHeaderHeight = 55
const tablePaginationHeight = 57
const publicationsPageHeight = `calc(100vh - ${pageVerticalPadding}px)`
const publicationsTableScrollY = `calc(100vh - ${pageVerticalPadding + toolbarHeight + tableHeaderHeight + tablePaginationHeight + tableCardChromeHeight}px)`

export function Publications() {
  const { t } = useTranslation()
  const isAdmin = useAppStore((state) => state.userInfo?.isAdmin === true)

  const [loading, setLoading] = useState(true)
  const [items, setItems] = useState<PublicationReviewItem[]>([])
  const [stateFilter, setStateFilter] = useState<StateFilter>('all')
  const [resourceTypeFilter, setResourceTypeFilter] = useState<ResourceTypeFilter>('all')

  /** 驳回弹窗：需填写审批意见 */
  const [rejectOpen, setRejectOpen] = useState(false)
  const [rejectTarget, setRejectTarget] = useState<PublicationReviewItem | null>(null)
  const [rejectComment, setRejectComment] = useState('')
  const [submitting, setSubmitting] = useState(false)

  const load = useCallback(
    async (state = stateFilter, resourceType = resourceTypeFilter) => {
      setLoading(true)
      try {
        const list = await getPublicationList({
          state: state === 'all' ? undefined : state,
          resourceType: resourceType === 'all' ? undefined : resourceType,
        })
        setItems(list)
      } catch {
        // 错误已由全局请求中间件统一提示
      } finally {
        setLoading(false)
      }
    },
    [stateFilter, resourceTypeFilter],
  )

  useEffect(() => {
    if (isAdmin) {
      void load('all', 'all')
    }
    // 首次进入按初始条件拉取，后续由交互触发
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isAdmin])

  const handleStateChange = (value: StateFilter) => {
    setStateFilter(value)
    void load(value, resourceTypeFilter)
  }

  const handleResourceTypeChange = (value: ResourceTypeFilter) => {
    setResourceTypeFilter(value)
    void load(stateFilter, value)
  }

  const handleApprove = async (record: PublicationReviewItem) => {
    try {
      await reviewPublication(String(record.publicationId), true)
      feedback.success(t('publications.approveSuccess'))
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const openReject = (record: PublicationReviewItem) => {
    setRejectTarget(record)
    setRejectComment('')
    setRejectOpen(true)
  }

  const handleReject = async () => {
    if (!rejectTarget?.publicationId) return
    setSubmitting(true)
    try {
      await reviewPublication(String(rejectTarget.publicationId), false, rejectComment.trim() || undefined)
      feedback.success(t('publications.rejectSuccess'))
      setRejectOpen(false)
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSubmitting(false)
    }
  }

  const renderResourceType = (type: PublicationResourceType | null | undefined) => {
    if (type === 'prompt') return <Tag color="purple">{t('publications.typePrompt')}</Tag>
    if (type === 'skill') return <Tag color="gold">{t('publications.typeSkill')}</Tag>
    return <Tag color="blue">{t('publications.typeApp')}</Tag>
  }

  const columns: TableColumnsType<PublicationReviewItem> = useMemo(
    () => [
      {
        title: t('publications.colResource'),
        key: 'resource',
        width: 260,
        render: (_, record) => (
          <Space size={spacing.xs}>
            {renderResourceType(record.resourceType)}
            <Avatar shape="square" size={28} src={record.resourceAvatar ? resolveStorageUrl(record.resourceAvatar) : undefined}>
              {(record.resourceName || record.resourceId || '?').slice(0, 1).toUpperCase()}
            </Avatar>
            <Text strong>{record.resourceName || record.resourceId || '-'}</Text>
          </Space>
        ),
      },
      {
        title: t('publications.colVersion'),
        dataIndex: 'publishVersion',
        width: 90,
        render: (v: number | null | undefined) => (v == null ? '-' : <Tag>v{formatAppVersion(v)}</Tag>),
      },
      { title: t('publications.colTeam'), dataIndex: 'teamName', width: 150, render: (v: string | null) => v || '-' },
      {
        title: t('publications.colCreate'),
        key: 'create',
        width: 150,
        render: (_, record) => <AuditCell user={record.createUserName} time={record.createTime} />,
      },
      {
        title: t('publications.colApplyReason'),
        dataIndex: 'applyReason',
        width: 200,
        ellipsis: true,
        render: (v: string | null) => v || '-',
      },
      {
        title: t('publications.colState'),
        key: 'state',
        width: 100,
        render: (_, record) => (
          <Tag color={STATE_TAG_COLOR[record.state ?? 'pending']}>
            {t(`publications.state_${record.state ?? 'pending'}`)}
          </Tag>
        ),
      },
      {
        title: t('publications.colReviewComment'),
        dataIndex: 'reviewComment',
        width: 200,
        ellipsis: true,
        render: (v: string | null) => v || '-',
      },
      {
        title: t('publications.colReviewInfo'),
        key: 'reviewInfo',
        width: 220,
        render: (_, record) => {
          if (record.state === 'pending' || !record.reviewTime) return <Text type="secondary">-</Text>
          return <AuditCell user={record.updateUserName} time={record.reviewTime} />
        },
      },
      {
        title: t('publications.colActions'),
        key: 'actions',
        width: 170,
        fixed: 'right',
        render: (_, record) => {
          if (record.state !== 'pending') return <Text type="secondary">-</Text>
          return (
            <Space size={0}>
              <Popconfirm title={t('publications.approveConfirm')} onConfirm={() => void handleApprove(record)}>
                <Button type="link" size="small" icon={<CheckOutlined />}>{t('publications.approve')}</Button>
              </Popconfirm>
              <Button type="link" size="small" danger icon={<CloseOutlined />} onClick={() => openReject(record)}>
                {t('publications.reject')}
              </Button>
            </Space>
          )
        },
      },
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, stateFilter, resourceTypeFilter],
  )

  if (!isAdmin) {
    return <Navigate to="/apps" replace />
  }

  return (
    <Page>
      <div style={{ display: 'flex', flexDirection: 'column', height: publicationsPageHeight, minHeight: 0 }}>
        <PageToolbar
          filters={
            <Space size={spacing.xs} wrap>
              <Select<StateFilter>
                value={stateFilter}
                onChange={handleStateChange}
                style={{ width: 140 }}
                options={[
                  { label: t('publications.stateAll'), value: 'all' },
                  { label: t('publications.state_pending'), value: 'pending' },
                  { label: t('publications.state_approved'), value: 'approved' },
                  { label: t('publications.state_rejected'), value: 'rejected' },
                ]}
              />
              <Select<ResourceTypeFilter>
                value={resourceTypeFilter}
                onChange={handleResourceTypeChange}
                style={{ width: 140 }}
                options={[
                  { label: t('publications.typeAll'), value: 'all' },
                  { label: t('publications.typeApp'), value: 'app' },
                  { label: t('publications.typePrompt'), value: 'prompt' },
                  { label: t('publications.typeSkill'), value: 'skill' },
                ]}
              />
            </Space>
          }
          queries={
            <Tooltip title={t('publications.refresh')}>
              <Button
                icon={<ReloadOutlined />}
                aria-label={t('publications.refresh')}
                onClick={() => void load()}
              />
            </Tooltip>
          }
        />
        <div style={{ flex: 1, minHeight: 0 }}>
          <DataTable<PublicationReviewItem>
            rowKey="publicationId"
            columns={columns}
            dataSource={items}
            loading={loading}
            sticky
            scroll={{ x: 1540, y: publicationsTableScrollY }}
            pagination={{
              showSizeChanger: true,
              pageSizeOptions: [10, 20, 50],
              defaultPageSize: 20,
            }}
          />
        </div>
      </div>
      <Modal
        open={rejectOpen}
        title={t('publications.rejectTitle', { name: rejectTarget?.resourceName ?? '' })}
        onCancel={() => setRejectOpen(false)}
        onOk={() => void handleReject()}
        confirmLoading={submitting}
        okButtonProps={{ danger: true }}
        okText={t('publications.reject')}
        cancelText={t('publications.cancel')}
        maskClosable={false}
        destroyOnHidden
      >
        <Input.TextArea
          value={rejectComment}
          onChange={(e) => setRejectComment(e.target.value)}
          placeholder={t('publications.rejectCommentPlaceholder')}
          maxLength={255}
          rows={3}
        />
      </Modal>
    </Page>
  )
}
