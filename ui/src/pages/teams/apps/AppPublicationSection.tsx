import { useCallback, useEffect, useMemo, useState } from 'react'
import { ArrowDownOutlined, RollbackOutlined } from '@ant-design/icons'
import { Alert, Button, Form, Input, Modal, Popconfirm, Space, Tag, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { AuditCell, Card as DSCard, DataTable, feedback } from '@/design-system'
import { fontSize, spacing } from '@/design-system/theme'
import { formatDateTime } from '@/utils/datetime'
import { APP_DEFAULT_VERSION, APP_VERSION_PATTERN, formatAppVersion, nextAppVersion, parseAppVersion } from '@/utils/appVersion'
import { publishApp, unpublishApp } from '@/api/app'
import {
  applyPublication,
  getTeamPublicationList,
  takedownPublication,
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
  /** 配置状态变化上报（团队应用卡发布/重新发布成功后回写 1） */
  onConfigStatusChange?: (status: number) => void
  onReload: () => Promise<void> | void
}

/**
 * 应用「上架管理」分区：发布与上架统一入口。
 * 「团队应用」卡——团队发布状态（线上发布快照/发布时间/当前版本），未发布提供「发布」、已发布提供「重新发布」（发布弹窗内设置版本号，
 * 版本号只能递增不能回退；发布即把当前草稿推入快照，幂等可重发）与「撤回发布」（撤回时已上架的应用自动下架）；
 * 「上架管理（应用市场）」卡——公开（is_public）需系统管理员审批，未公开可「申请上架」（无需填版本号，自动记录应用当前发布版本）、
 * 已公开可「下架」；「申请记录」卡展示历次申请。成员只读。
 * 外部应用（detail.isExternal）无上架审批概念：仅渲染「团队应用」发布卡，直接发布即生效，不加载申请记录。
 */
export function AppPublicationSection({ teamId, appId, detail, canManage, configStatus, onConfigStatusChange, onReload }: AppPublicationSectionProps) {
  const { t } = useTranslation()

  const [publicationItems, setPublicationItems] = useState<PublicationReviewItem[]>([])
  const [publicationLoading, setPublicationLoading] = useState(false)
  const [applyOpen, setApplyOpen] = useState(false)
  const [applying, setApplying] = useState(false)
  const [unpublishing, setUnpublishing] = useState(false)
  const [takedowning, setTakedowning] = useState(false)
  const [publishOpen, setPublishOpen] = useState(false)
  const [publishing, setPublishing] = useState(false)
  const [applyForm] = Form.useForm<{ applyReason?: string }>()
  const [publishForm] = Form.useForm<{ version: string }>()

  // 外部应用：无上架审批概念（不公开到平台），分区收敛为仅发布
  const isExternal = detail?.isExternal === true

  const loadPublications = useCallback(async () => {
    // 外部应用不公开到平台，无上架申请记录可查
    if (isExternal) return
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
  }, [teamId, appId, isExternal])

  useEffect(() => {
    void loadPublications()
  }, [loadPublications])

  const pendingPublication = publicationItems.find((x) => x.state === 'pending')
  const lastRejected = publicationItems.find((x) => x.state === 'rejected')

  /** 打开发布弹窗：版本号只在团队发布时设置，必须严格大于当前版本，默认自动填入下一版本号（可手改） */
  const openPublishModal = () => {
    const current = detail?.version ?? APP_DEFAULT_VERSION
    const next = nextAppVersion(current)
    publishForm.setFieldsValue({ version: next != null ? formatAppVersion(next) : '' })
    setPublishOpen(true)
  }

  /** 发布/重新发布共用同一接口同一弹窗：未发布即首次发布，已发布把当前草稿推入发布快照并按弹窗版本号更新版本 */
  const handlePublish = async () => {
    if (!appId) return
    let values: { version: string }
    try {
      values = await publishForm.validateFields()
    } catch {
      return // 表单校验未通过，antd 已就地提示
    }
    setPublishing(true)
    const republish = detail?.publishStatus === 1
    try {
      await publishApp(appId, values.version.trim())
      feedback.success(t(republish ? 'appManage.republishSuccess' : 'appManage.publishSuccess'))
      // 发布即把当前草稿写入快照，配置状态回「一致」
      onConfigStatusChange?.(1)
      setPublishOpen(false)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      // 与上架管理有联动：操作后即时刷新详情与申请记录数据（不整页刷新）
      await Promise.all([loadPublications(), onReload()])
      setPublishing(false)
    }
  }

  /** 打开申请弹窗：上架把最新的团队发布上架，版本号自动记录应用当前发布版本，无需填写 */
  const openApplyModal = () => {
    applyForm.setFieldsValue({ applyReason: '' })
    setApplyOpen(true)
  }

  const handleApplyPublication = async () => {
    let values: { applyReason?: string }
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
      })
      feedback.success(t('appManage.applySuccess'))
      setApplyOpen(false)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      await Promise.all([loadPublications(), onReload()])
      setApplying(false)
    }
  }

  const handleWithdrawPublication = async () => {
    if (!pendingPublication?.publicationId) return
    try {
      await withdrawPublication(pendingPublication.publicationId)
      feedback.success(t('appManage.withdrawSuccess'))
    } catch {
      // 错误已由全局请求中间件统一提示（含申请已被联动撤下等陈旧操作）
    } finally {
      // 无论成败都刷新列表与详情：陈旧按钮点击后状态自愈
      await Promise.all([loadPublications(), onReload()])
    }
  }

  /** 撤回发布（取消团队发布）：成员将无法再进入对话；后端会联动下架并撤下待审申请，需一并刷新申请记录 */
  const handleUnpublish = async () => {
    try {
      await unpublishApp(appId)
      feedback.success(t('appManage.unpublishSuccess'))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      await Promise.all([loadPublications(), onReload()])
      setUnpublishing(false)
    }
  }

  /** 下架（取消公开）：平台内用户不再可用，不影响团队内发布状态 */
  const handleTakedown = async () => {
    try {
      await takedownPublication({ resourceType: 'app', resourceId: appId })
      feedback.success(t('appManage.takedownSuccess'))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      await Promise.all([loadPublications(), onReload()])
      setTakedowning(false)
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
        title: t('appManage.colCreate'),
        key: 'create',
        width: 150,
        render: (_, record) => <AuditCell user={record.createUserName} time={record.createTime} />,
      },
      {
        title: t('appManage.publicationColReviewInfo'),
        key: 'reviewInfo',
        width: 150,
        render: (_, record) => {
          if (record.state === 'pending') return <Text type="secondary">-</Text>
          return <AuditCell user={record.updateUserName} time={record.reviewTime} />
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
                    <Button type="link" size="small" icon={<RollbackOutlined />}>
                      {t('appManage.withdrawApplication')}
                    </Button>
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
  // 应用市场当前发布版本号：最近一次审批通过的上架申请所带版本
  const marketVersion = formatAppVersion(publicationItems.find((x) => x.state === 'approved')?.publishVersion)

  return (
    <Space direction="vertical" size={spacing.md} style={{ width: '100%' }}>
      {/* 卡片一：团队应用——团队发布状态与发布入口（发布/重新发布带版本号弹窗、撤回发布）；发布=团队内可用最新版；
          外部应用仅此一卡：直接发布即生效，外部用户通过访问点使用已发布版本 */}
      <DSCard title={t(isExternal ? 'appManage.externalPublishCard' : 'appManage.teamAppCard')}>
        <Space direction="vertical" size={spacing.sm} style={{ width: '100%' }}>
          {isExternal && <Text type="secondary" style={{ fontSize: fontSize.xs }}>{t('appManage.externalPublishHint')}</Text>}
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
                {canManage && (
                  <Button size="small" type="primary" onClick={openPublishModal}>
                    {t('appManage.republish')}
                  </Button>
                )}
                {canManage && (
                  <Popconfirm
                    title={t(isExternal ? 'appManage.externalUnpublishConfirm' : 'appManage.unpublishConfirm')}
                    okText={t('appManage.confirm')}
                    cancelText={t('appManage.cancel')}
                    onConfirm={() => {
                      setUnpublishing(true)
                      void handleUnpublish()
                    }}
                  >
                    <Button size="small" danger icon={<RollbackOutlined />} loading={unpublishing}>
                      {t('appManage.unpublish')}
                    </Button>
                  </Popconfirm>
                )}
              </>
            ) : (
              <>
                <Tag>{t('appManage.unpublished')}</Tag>
                {canManage && (
                  <Button size="small" type="primary" onClick={openPublishModal}>
                    {t('appManage.publish')}
                  </Button>
                )}
              </>
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
      {/* 卡片二：上架管理（应用市场）——公开状态、市场发布版本号与下架（上架=全平台可用）；外部应用不公开到平台，隐藏此卡与申请记录 */}
      {!isExternal && (
        <>
      <DSCard title={t('appManage.sectionPublication')}>
        <Space direction="vertical" size={spacing.md} style={{ width: '100%' }}>
          <Text type="secondary">{t('appManage.publicationHint')}</Text>
          <Space size={spacing.sm} wrap>
            {detail?.isPublic ? (
              <>
                <Tag color="green">{t('appManage.publicOn')}</Tag>
                {marketVersion && (
                  <>
                    <Text type="secondary">{t('appManage.publicationMarketVersion')}</Text>
                    <Tag color="blue">v{marketVersion}</Tag>
                  </>
                )}
                {canManage && (
                  <Popconfirm
                    title={t('appManage.takedownConfirm')}
                    okText={t('appManage.confirm')}
                    cancelText={t('appManage.cancel')}
                    onConfirm={() => {
                      setTakedowning(true)
                      void handleTakedown()
                    }}
                  >
                    <Button size="small" danger icon={<ArrowDownOutlined />} loading={takedowning}>
                      {t('appManage.takedown')}
                    </Button>
                  </Popconfirm>
                )}
              </>
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
        </>
      )}
      {!isExternal && (
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
            label={t('appManage.version')}
            extra={t('appManage.applyVersionHint', { version: formatAppVersion(detail?.version) || '1.0.0' })}
          >
            <Tag color="blue">v{formatAppVersion(detail?.version) || '1.0.0'}</Tag>
          </Form.Item>
          <Form.Item name="applyReason" label={t('appManage.applyReasonLabel')} rules={[{ max: 255 }]}>
            <Input.TextArea placeholder={t('appManage.applyReasonPlaceholder')} maxLength={255} rows={3} />
          </Form.Item>
        </Form>
      </Modal>
      )}
      {/* 发布弹窗（自工作台头部迁入）：版本号只在团队发布时设置，只能递增不能回退 */}
      <Modal
        open={publishOpen}
        title={detail?.publishStatus === 1 ? t('appManage.republish') : t('appManage.publish')}
        onCancel={() => setPublishOpen(false)}
        confirmLoading={publishing}
        onOk={() => void handlePublish()}
        okText={t('appManage.confirm')}
        cancelText={t('appManage.cancel')}
        maskClosable={false}
        destroyOnHidden
      >
        <Text type="secondary" style={{ display: 'block', marginBottom: spacing.md, fontSize: 12 }}>
          {t('appManage.publishVersionHint')}
        </Text>
        <Form form={publishForm} layout="vertical">
          <Form.Item
            name="version"
            label={t('appManage.version')}
            rules={[
              { required: true, message: t('appManage.versionRequired') },
              { pattern: APP_VERSION_PATTERN, message: t('appManage.versionInvalid') },
              // 版本号必须严格递增（int 编码直接比较）：相同或低于当前版本就地拦截（不携带版本号的快捷重发不受影响）
              {
                validator: (_, value: string) => {
                  const next = parseAppVersion(value)
                  const current = detail?.version ?? APP_DEFAULT_VERSION
                  if (next != null && next <= current) {
                    return Promise.reject(new Error(t('appManage.versionMustIncrease', { version: formatAppVersion(current) || '1.0.0' })))
                  }
                  return Promise.resolve()
                },
              },
            ]}
          >
            <Input placeholder={t('appManage.versionPlaceholder')} maxLength={12} allowClear />
          </Form.Item>
        </Form>
      </Modal>
    </Space>
  )
}
