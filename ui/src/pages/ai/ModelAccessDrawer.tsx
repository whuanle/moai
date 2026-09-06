import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  Button,
  Drawer,
  Form,
  InputNumber,
  Modal,
  Popconfirm,
  Select,
  Space,
  Switch,
  Tag,
  Tooltip,
  Typography,
} from 'antd'
import type { TableColumnsType } from 'antd'
import { DeleteOutlined, EditOutlined, PlusOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import {
  aichannelApi,
  type AIModelItem,
  type ModelAuthorization,
  type ModelQuotaInfo,
} from '@/api/aichannel'
import { getAllTeams, type AllTeamItem } from '@/api/team'
import { DataTable, feedback } from '@/design-system'

/** 重置周期单位选项，与后端 PeriodUnit 约定一致：0=不重置 1=小时 2=天 3=周 4=月. */
const PERIOD_UNIT_OPTIONS = [1, 2, 3, 4, 0] as const

function periodUnitLabelKey(unit: number): string {
  switch (unit) {
    case 1:
      return 'models.periodUnitHour'
    case 2:
      return 'models.periodUnitDay'
    case 3:
      return 'models.periodUnitWeek'
    case 4:
      return 'models.periodUnitMonth'
    default:
      return 'models.periodUnitNone'
  }
}

/** 生成额度摘要，例如「每 1 天 100000 tokens」/「总量 100000 tokens」. */
function quotaSummary(quota: ModelQuotaInfo, t: (k: string, o?: Record<string, unknown>) => string): string {
  if ((quota.periodUnit ?? 0) === 0) {
    return t('models.quotaSummaryOnce', { limit: quota.limitValue })
  }
  return t('models.quotaSummaryPeriod', {
    limit: quota.limitValue,
    value: quota.periodValue,
    unit: t(periodUnitLabelKey(quota.periodUnit ?? 0)),
  })
}

interface QuotaFormValues {
  enabled?: boolean
  periodUnit: number
  periodValue?: number
  limitValue?: number
}

interface ModelAccessDrawerProps {
  model: AIModelItem | null
  open: boolean
  onClose: () => void
  /** 可见性变更后通知父级刷新模型列表. */
  onVisibilityChanged: () => void
}

/** 额度编辑弹窗：公开模型（teamId=0）与私有模型按团队共用；关闭「启用额度」提交后即移除规则. */
function QuotaModal({
  open,
  teamId,
  teamName,
  initial,
  saving,
  onOk,
  onCancel,
}: {
  open: boolean
  teamId: number
  teamName: string
  initial: ModelQuotaInfo | null
  saving: boolean
  /** limitValue=-1 表示移除额度规则. */
  onOk: (values: { periodUnit: number; periodValue: number; limitValue: number }) => void
  onCancel: () => void
}) {
  const { t } = useTranslation()
  const [form] = Form.useForm<QuotaFormValues>()
  const enabled = Form.useWatch('enabled', form) ?? Boolean(initial)
  const periodUnit = Form.useWatch('periodUnit', form) ?? initial?.periodUnit ?? 2

  useEffect(() => {
    if (!open) return
    form.setFieldsValue({
      enabled: Boolean(initial),
      periodUnit: initial?.periodUnit ?? 2,
      periodValue: initial?.periodValue ?? 1,
      // 后端 long 序列化为 string，表单内统一转 number
      limitValue: initial ? Number(initial.limitValue) : undefined,
    })
  }, [open, initial, form])

  return (
    <Modal
      open={open}
      title={`${t('models.setQuota')} - ${teamName}`}
      okText={t('models.save')}
      cancelText={t('models.cancel')}
      confirmLoading={saving}
      destroyOnClose
      maskClosable={false}
      onOk={async () => {
        if (!enabled) {
          onOk({ periodUnit: initial?.periodUnit ?? 0, periodValue: initial?.periodValue ?? 0, limitValue: -1 })
          return
        }
        const values = await form.validateFields()
        onOk({
          periodUnit: values.periodUnit,
          periodValue: values.periodUnit === 0 ? 1 : (values.periodValue ?? 1),
          limitValue: values.limitValue ?? 0,
        })
      }}
      onCancel={onCancel}
    >
      <Form form={form} layout="vertical">
        <Form.Item
          name="enabled"
          label={teamId === 0 ? t('models.globalQuotaTitle') : t('models.quotaEnabled')}
          valuePropName="checked"
        >
          <Switch />
        </Form.Item>
        {enabled && (
          <>
            <Space size={16} wrap>
              <Form.Item name="periodUnit" label={t('models.quotaPeriodUnit')} rules={[{ required: true }]}>
                <Select
                  style={{ width: 180 }}
                  options={PERIOD_UNIT_OPTIONS.map((u) => ({ value: u, label: t(periodUnitLabelKey(u)) }))}
                />
              </Form.Item>
              {periodUnit !== 0 && (
                <Form.Item
                  name="periodValue"
                  label={t('models.quotaPeriodValue')}
                  rules={[{ required: true, message: t('models.quotaPeriodValue') }]}
                >
                  <InputNumber min={1} precision={0} style={{ width: 120 }} />
                </Form.Item>
              )}
            </Space>
            <Form.Item
              name="limitValue"
              label={t('models.quotaLimit')}
              rules={[{ required: true, message: t('models.quotaLimit') }]}
            >
              <InputNumber min={1} precision={0} style={{ width: 220 }} />
            </Form.Item>
          </>
        )}
      </Form>
    </Modal>
  )
}

/** 模型授权与额度抽屉：私有模型管理授权团队与各团队额度；公开模型管理全局共享额度. */
export function ModelAccessDrawer({ model, open, onClose, onVisibilityChanged }: ModelAccessDrawerProps) {
  const { t } = useTranslation()
  const [authorization, setAuthorization] = useState<ModelAuthorization | null>(null)
  const [loading, setLoading] = useState(false)
  const [teams, setTeams] = useState<AllTeamItem[]>([])
  const [submitting, setSubmitting] = useState(false)
  const [selectedTeamIds, setSelectedTeamIds] = useState<number[]>([])
  const [quotaModal, setQuotaModal] = useState<{ open: boolean; teamId: number; teamName: string }>({
    open: false,
    teamId: 0,
    teamName: '',
  })

  const isPublic = authorization?.isPublic ?? model?.isPublic ?? true
  const modelId = model?.id ?? null
  /** 归一化后的授权团队行（后端字段可空，统一收敛为稳定类型）. */
  const authorizedItems = useMemo(
    () =>
      (authorization?.items ?? []).map((x) => ({
        teamId: Number(x.teamId ?? 0),
        teamName: String(x.teamName ?? ''),
        quota: x.quota ?? null,
      })),
    [authorization],
  )

  const loadAuthorization = useCallback(async () => {
    if (!model?.id) return
    setLoading(true)
    try {
      setAuthorization(await aichannelApi.getModelAuthorization(model.id))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [model?.id])

  useEffect(() => {
    if (!open) return
    void loadAuthorization()
  }, [open, loadAuthorization])

  useEffect(() => {
    if (!open || isPublic) return
    // 私有模型才需要候选团队列表
    getAllTeams()
      .then(setTeams)
      .catch(() => setTeams([]))
  }, [open, isPublic])

  const runAction = async (action: () => Promise<void>, successKey: string) => {
    setSubmitting(true)
    try {
      await action()
      feedback.success(t(successKey))
      await loadAuthorization()
      return true
    } catch {
      // 错误已由全局请求中间件统一提示
      return false
    } finally {
      setSubmitting(false)
    }
  }

  const handleVisibilityChange = async (checked: boolean) => {
    if (!modelId) return
    await runAction(() => aichannelApi.updateModelVisibility(modelId, checked), 'models.saveSuccess')
    onVisibilityChanged()
  }

  const handleAddTeams = async () => {
    if (!modelId || selectedTeamIds.length === 0) return
    const nextTeamIds = [...authorizedItems.map((x) => x.teamId), ...selectedTeamIds]
    const ok = await runAction(
      () => aichannelApi.updateModelAuthorization(modelId, nextTeamIds),
      'models.authAddSuccess',
    )
    if (ok) setSelectedTeamIds([])
  }

  const handleRemoveTeam = async (teamId: number) => {
    if (!modelId) return
    const nextTeamIds = authorizedItems.map((x) => x.teamId).filter((id) => id !== teamId)
    await runAction(() => aichannelApi.updateModelAuthorization(modelId, nextTeamIds), 'models.authRemoveSuccess')
  }

  const handleQuotaOk = async (
    target: { modelId: string; teamId: number },
    values: { periodUnit: number; periodValue: number; limitValue: number },
  ) => {
    const remove = values.limitValue < 0
    const ok = await runAction(
      () =>
        remove
          ? aichannelApi.deleteModelQuota(target.modelId, target.teamId)
          : aichannelApi.updateModelQuota(target.modelId, target.teamId, values),
      remove ? 'models.quotaRemoveSuccess' : 'models.quotaSaveSuccess',
    )
    if (ok) setQuotaModal({ open: false, teamId: 0, teamName: '' })
  }

  const teamOptions = useMemo(
    () =>
      teams
        .filter((team) => !authorizedItems.some((item) => item.teamId === Number(team.teamId)))
        .map((team) => ({
          value: Number(team.teamId),
          label: `${team.name}${team.isDisable ? `（${t('models.teamDisabled')}）` : ''}`,
        })),
    [teams, authorizedItems, t],
  )

  const columns: TableColumnsType<(typeof authorizedItems)[number]> = [
    { title: t('models.authColTeam'), dataIndex: 'teamName', width: 160 },
    {
      title: t('models.quotaColPeriod'),
      key: 'period',
      width: 180,
      render: (_, record) =>
        record.quota
          ? quotaSummary(record.quota, t)
          : <Typography.Text type="secondary">{t('models.quotaNoLimit')}</Typography.Text>,
    },
    {
      title: t('models.quotaColUsed'),
      dataIndex: ['quota', 'usedTokens'],
      width: 120,
      render: (v: string | number | null | undefined) => (v == null ? '-' : String(v)),
    },
    {
      title: t('models.colActions'),
      key: 'actions',
      width: 130,
      render: (_, record) => (
        <Space size={0}>
          <Tooltip title={record.quota ? t('models.editQuota') : t('models.setQuota')}>
            <Button
              type="text"
              size="small"
              icon={<EditOutlined />}
              aria-label={t('models.setQuota')}
              onClick={() =>
                setQuotaModal({ open: true, teamId: record.teamId, teamName: record.teamName })
              }
            />
          </Tooltip>
          <Popconfirm
            title={t('models.authRemoveConfirm')}
            okButtonProps={{ danger: true }}
            onConfirm={() => void handleRemoveTeam(record.teamId)}
          >
            <Tooltip title={t('models.authRemove')}>
              <Button type="text" size="small" danger icon={<DeleteOutlined />} aria-label={t('models.authRemove')} />
            </Tooltip>
          </Popconfirm>
        </Space>
      ),
    },
  ]

  return (
    <Drawer
      open={open}
      onClose={onClose}
      title={
        <Space size={8} wrap>
          <span>{model?.name}</span>
          <Tag color="geekblue">{t('models.accessTitle')}</Tag>
        </Space>
      }
      width={720}
      destroyOnClose
    >
      {modelId && model && (
        <>
          <Space size={12} wrap style={{ marginBottom: 16 }}>
            <Typography.Text strong>{t('models.isPublic')}</Typography.Text>
            <Switch checked={isPublic} loading={submitting} onChange={handleVisibilityChange} />
            <Typography.Text type="secondary">{t('models.visibilityHint')}</Typography.Text>
          </Space>

          {isPublic ? (
            <div>
              <Typography.Title level={5}>{t('models.globalQuotaTitle')}</Typography.Title>
              {authorization?.globalQuota ? (
                <Space size={12} wrap style={{ marginBottom: 16 }}>
                  <Tag>{quotaSummary(authorization.globalQuota, t)}</Tag>
                  <span>
                    {t('models.quotaColUsed')}: {authorization.globalQuota.usedTokens}
                  </span>
                  <Button size="small" onClick={() => setQuotaModal({ open: true, teamId: 0, teamName: t('models.public') })}>
                    {t('models.editQuota')}
                  </Button>
                  <Popconfirm
                    title={t('models.quotaRemoveConfirm')}
                    okButtonProps={{ danger: true }}
                    onConfirm={() =>
                      modelId && void handleQuotaOk({ modelId, teamId: 0 }, { periodUnit: 0, periodValue: 0, limitValue: -1 })
                    }
                  >
                    <Button size="small" danger>
                      {t('models.quotaRemove')}
                    </Button>
                  </Popconfirm>
                </Space>
              ) : (
                <Space size={12} wrap style={{ marginBottom: 16 }}>
                  <Typography.Text type="secondary">{t('models.quotaNoLimit')}</Typography.Text>
                  <Button
                    type="primary"
                    size="small"
                    icon={<PlusOutlined />}
                    onClick={() => setQuotaModal({ open: true, teamId: 0, teamName: t('models.public') })}
                  >
                    {t('models.setQuota')}
                  </Button>
                </Space>
              )}
            </div>
          ) : (
            <>
              {authorizedItems.length === 0 && !loading && (
                <Typography.Paragraph type="secondary">{t('models.authNoTeams')}</Typography.Paragraph>
              )}
              <DataTable<(typeof authorizedItems)[number]>
                rowKey="teamId"
                columns={columns}
                dataSource={authorizedItems}
                loading={loading}
                pagination={false}
                size="small"
                toolbar={
                  <Space size={12} wrap>
                    <Select
                      mode="multiple"
                      style={{ minWidth: 260 }}
                      placeholder={t('models.authSelectTeamPlaceholder')}
                      options={teamOptions}
                      value={selectedTeamIds}
                      onChange={(v) => setSelectedTeamIds(v)}
                      allowClear
                    />
                    <Button
                      type="primary"
                      icon={<PlusOutlined />}
                      disabled={selectedTeamIds.length === 0}
                      loading={submitting}
                      onClick={() => void handleAddTeams()}
                    >
                      {t('models.authAddTeam')}
                    </Button>
                  </Space>
                }
                onRefresh={loadAuthorization}
                refreshLoading={loading}
              />
            </>
          )}
        </>
      )}

      <QuotaModal
        open={quotaModal.open}
        teamId={quotaModal.teamId}
        teamName={quotaModal.teamName}
        initial={
          quotaModal.teamId === 0
            ? authorization?.globalQuota ?? null
            : authorizedItems.find((x) => x.teamId === quotaModal.teamId)?.quota ?? null
        }
        saving={submitting}
        onOk={(values) =>
          modelId && void handleQuotaOk({ modelId, teamId: quotaModal.teamId }, values)
        }
        onCancel={() => setQuotaModal({ open: false, teamId: 0, teamName: '' })}
      />
    </Drawer>
  )
}
