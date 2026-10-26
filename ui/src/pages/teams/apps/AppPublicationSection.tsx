import { useCallback, useEffect, useMemo, useState } from 'react'
import { Alert, Button, Form, Input, Modal, Popconfirm, Space, Tag, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { Card as DSCard, DataTable, feedback } from '@/design-system'
import { fontSize, spacing } from '@/design-system/theme'
import { formatDateTime } from '@/utils/datetime'
import { APP_VERSION_PATTERN, formatAppVersion } from '@/utils/appVersion'
import {
  applyPublication,
  getTeamPublicationList,
  withdrawPublication,
  type PublicationReviewItem,
  type PublicationState,
} from '@/api/publication'
import type { AppDetail } from './AppConfigSection'

const { Text } = Typography

const STATE_TAG_COLOR: Record<PublicationState, string> = {
  pending: 'orange',
  approved: 'green',
  rejected: 'error',
}

export interface AppPublicationSectionProps {
  teamId: number
  appId: string
  detail: AppDetail | null
  canManage: boolean
  /** 配置状态（工作台持有的真值）：0=草稿有未发布变更 1=与线上一致；null=未知 */
  configStatus?: number | null
  onReload: () => Promise<void> | void
}

/**
 * 应用「上架管理」分区：上架状态概览（公开/审核中/被驳回与拒绝原因）、
 * 版本与草稿状态（线上发布快照 vs 当前草稿）、历次申请记录。
 * 公开（is_public）需系统管理员审批，团队管理员可申请/撤回/重新申请，成员只读。
 */
export function AppPublicationSection({ teamId, appId, detail, canManage, configStatus, onReload }: AppPublicationSectionProps) {
  const { t } = useTranslation()

  const [publicationItems, setPublicationItems] = useState<PublicationReviewItem[]>([])
  const [publicationLoading, setPublicationLoading] = useState(false)
  const [applyOpen, setApplyOpen] = useState(false)
  const [applying, setApplying] = useState(false)
  const [applyForm] = Form.useForm<{ version: string; applyReason?: string }>()

  const loadPublications = useCallback(async () => {
    if (!Number.isFinite(teamId) || teamId <= 0) return
    setPublicationLoading(true)
    try {
      const list = await getTeamPublicationList(teamId, { resourceType: 'app' })
      // 后端按申请时间倒序返回，首条即最近一次申请
      setPublicationItems(list.filter((x) => x.resourceId === appId))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setPublicationLoading(false)
    }
  }, [teamId, appId])

  useEffect(() => {
    void loadPublications()
  }, [loadPublications])

  const pendingPublication = publicationItems.find((x) => x.state === 'pending')
  const lastRejected = publicationItems.find((x) => x.state === 'rejected')

  /** 打开申请弹窗：版本号默认带入当前草稿版本，可改为本次要上架的版本 */
  const openApplyModal = () => {
    applyForm.setFieldsValue({ version: formatAppVersion(detail?.version) || '1.0.0', applyReason: '' })
    setApplyOpen(true)
  }

  const handleApplyPublication = async () => {
    let values: { version: string; applyReason?: string }
    try {
      values = await applyForm.validateFields()
    } catch {
      return // 表单校验未通过，antd 已就地提示
    }
    setApplying(true)
    try {
      await applyPublication({
        resourceType: 'app',
        resourceId: appId,
        applyReason: values.applyReason?.trim() || undefined,
        version: values.version.trim(),
      })
      feedback.success(t('appManage.applySuccess'))
      setApplyOpen(false)
      await loadPublications()
      await onReload()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setApplying(false)
    }
  }

  const handleWithdrawPublication = async () => {
    if (!pendingPublication?.publicationId) return
    try {
      await withdrawPublication(pendingPublication.publicationId)
      feedback.success(t('appManage.withdrawSuccess'))
      await loadPublications()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const columns: TableColumnsType<PublicationReviewItem> = useMemo(
    () => [
      {
        title: t('appManage.publicationColState'),
        key: 'state',
        width: 100,
        render: (_, record) => (
          <Tag color={STATE_TAG_COLOR[record.state ?? 'pending']}>
            {t(`appManage.publicationState_${record.state ?? 'pending'}`)}
          </Tag>
        ),
      },
      {
        title: t('appManage.version'),
        dataIndex: 'publishVersion',
        width: 100,
        render: (v: number | null | undefined) => (v == null ? '-' : <Tag>v{formatAppVersion(v)}</Tag>),
      },
      {
        title: t('appManage.publicationColApplyReason'),
        dataIndex: 'applyReason',
        width: 200,
        ellipsis: true,
        render: (v: string | null) => v || '-',
      },
      {
        title: t('appManage.publicationColReviewComment'),
        dataIndex: 'reviewComment',
        width: 220,
        ellipsis: true,
        render: (v: string | null) => v || '-',
      },
      {
        title: t('appManage.publicationColApplicant'),
        dataIndex: 'createUserName',
        width: 110,
        render: (v: string | null) => v || '-',
      },
      {
        title: t('appManage.publicationColApplyTime'),
        dataIndex: 'createTime',
        width: 160,
        render: (v: string | null) => <span style={{ whiteSpace: 'nowrap' }}>{formatDateTime(v)}</span>,
      },
      {
        title: t('appManage.publicationColReviewInfo'),
        key: 'reviewInfo',
        width: 200,
        render: (_, record) => {
          if (record.state === 'pending' || !record.reviewTime) return <Text type="secondary">-</Text>
          return (
            <div style={{ lineHeight: 1.5 }}>
              <div>{record.updateUserName || '-'}</div>
              <Text type="secondary" style={{ fontSize: fontSize.xs }}>{formatDateTime(record.reviewTime)}</Text>
            </div>
          )
        },
      },
      ...(canManage
        ? [
            {
              title: t('appManage.publicationColActions'),
              key: 'actions',
              width: 110,
              render: (_: unknown, record: PublicationReviewItem) => {
                if (record.state !== 'pending' || !record.publicationId) return <Text type="secondary">-</Text>
                return (
                  <Popconfirm
                    title={t('appManage.withdrawConfirm')}
                    okText={t('appManage.confirm')}
                    cancelText={t('appManage.cancel')}
                    onConfirm={() => void handleWithdrawPublication()}
                  >
                    <Button size="small">{t('appManage.withdrawApplication')}</Button>
                  </Popconfirm>
                )
              },
            } satisfies TableColumnsType<PublicationReviewItem>[number],
          ]
        : []),
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, canManage, publicationItems],
  )

  const isPublished = detail?.publishStatus === 1
  const draftVersion = formatAppVersion(detail?.version)
  const liveVersion = formatAppVersion(detail?.publishVersion)
  // 已发布且快照版本号与当前草稿版本号不一致：版本改动尚未随发布生效
  const versionPending = isPublished && detail?.publishVersion != null && detail.publishVersion !== detail.version

  return (
    <Space direction="vertical" size={spacing.md} style={{ width: '100%' }}>
      <DSCard title={t('appManage.sectionPublication')}>
        <Space direction="vertical" size={spacing.md} style={{ width: '100%' }}>
          <Text type="secondary">{t('appManage.publicationHint')}</Text>
          <Space size={spacing.sm} wrap>
            {detail?.isPublic ? (
              <Tag color="green">{t('appManage.publicOn')}</Tag>
            ) : pendingPublication ? (
              <>
                <Tag color="orange">{t('appManage.publicationPending')}</Tag>
                {canManage && (
                  <Popconfirm
                    title={t('appManage.withdrawConfirm')}
                    okText={t('appManage.confirm')}
                    cancelText={t('appManage.cancel')}
                    onConfirm={() => void handleWithdrawPublication()}
                  >
                    <Button size="small" loading={publicationLoading}>
                      {t('appManage.withdrawApplication')}
                    </Button>
                  </Popconfirm>
                )}
              </>
            ) : (
              <>
                {lastRejected && <Tag color="error">{t('appManage.publicationRejected')}</Tag>}
                {!lastRejected && <Tag>{t('appManage.publicationNotApplied')}</Tag>}
                {canManage && (
                  <Button type="primary" onClick={openApplyModal}>
                    {lastRejected ? t('appManage.reapplyPublication') : t('appManage.applyPublication')}
                  </Button>
                )}
              </>
            )}
          </Space>
          {lastRejected && !detail?.isPublic && !pendingPublication && (
            <Alert
              type="error"
              showIcon
              message={t('appManage.publicationRejectionReason')}
              description={
                lastRejected.reviewComment || (
                  <Text type="secondary" style={{ fontSize: fontSize.xs }}>
                    {t('appManage.publicationNoComment')}
                  </Text>
                )
              }
            />
          )}
        </Space>
      </DSCard>
      <DSCard title={t('appManage.publicationVersionCard')}>
        <Space direction="vertical" size={spacing.sm} style={{ width: '100%' }}>
          <Space size={spacing.sm} wrap>
            <Text type="secondary">{t('appManage.publicationOnlineVersion')}</Text>
            {isPublished ? (
              <>
                <Tag color="green">{t('appManage.published')}</Tag>
                {liveVersion && <Tag color="blue">v{liveVersion}</Tag>}
                {detail?.publishTime && (
                  <Text type="secondary" style={{ fontSize: fontSize.xs }}>
                    {t('appManage.publicationPublishTime')}：{formatDateTime(detail.publishTime)}
                  </Text>
                )}
              </>
            ) : (
              <Tag>{t('appManage.unpublished')}</Tag>
            )}
          </Space>
          <Space size={spacing.sm} wrap>
            <Text type="secondary">{t('appManage.publicationDraft')}</Text>
            {draftVersion && <Tag>v{draftVersion}</Tag>}
            {configStatus === 0 && <Tag color="orange">{t('appManage.publicationDraftChanged')}</Tag>}
            {versionPending && <Tag color="orange">{t('appManage.publicationVersionUnreleased')}</Tag>}
            {configStatus !== 0 && !versionPending && <Tag>{t('appManage.publicationDraftInSync')}</Tag>}
            {configStatus === 0 && (
              <Text type="secondary" style={{ fontSize: fontSize.xs }}>
                {t('appManage.draftPendingHint')}
              </Text>
            )}
          </Space>
        </Space>
      </DSCard>
      <DSCard title={t('appManage.publicationRecords')}>
        <DataTable<PublicationReviewItem>
          rowKey="publicationId"
          columns={columns}
          dataSource={publicationItems}
          loading={publicationLoading}
          pagination={false}
          locale={{ emptyText: t('appManage.publicationRecordsEmpty') }}
        />
      </DSCard>
      <Modal
        open={applyOpen}
        title={t('appManage.applyPublication')}
        onCancel={() => setApplyOpen(false)}
        confirmLoading={applying}
        onOk={() => void handleApplyPublication()}
        okText={t('appManage.applySubmit')}
        cancelText={t('appManage.cancel')}
        maskClosable={false}
        destroyOnHidden
      >
        <Text type="secondary" style={{ display: 'block', marginBottom: spacing.md, fontSize: 12 }}>
          {t('appManage.applyHint')}
        </Text>
        <Form form={applyForm} layout="vertical">
          <Form.Item
            name="version"
            label={t('appManage.version')}
            rules={[
              { required: true, message: t('appManage.versionRequired') },
              { pattern: APP_VERSION_PATTERN, message: t('appManage.versionInvalid') },
            ]}
            extra={t('appManage.applyVersionHint', { version: formatAppVersion(detail?.version) || '1.0.0' })}
          >
            <Input placeholder={t('appManage.versionPlaceholder')} maxLength={12} allowClear />
          </Form.Item>
          <Form.Item name="applyReason" label={t('appManage.applyReasonLabel')} rules={[{ max: 255 }]}>
            <Input.TextArea placeholder={t('appManage.applyReasonPlaceholder')} maxLength={255} rows={3} />
          </Form.Item>
        </Form>
      </Modal>
    </Space>
  )
}
