import { useCallback, useEffect, useState } from 'react'
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { Alert, Button, Divider, Input, InputNumber, Popconfirm, Select, Space, Switch, Tag, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { Card as DSCard, DataTable, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import {
  getAppSecurity,
  getAppSecurityEvents,
  saveAppSecurity,
  SECURITY_RULE_TYPES,
  SECURITY_STRATEGIES,
  type AppSecurityEventItem,
  type AppSecurityRuleItem,
  type SecurityRuleType,
  type SecurityStrategy,
} from '@/api/app'

export interface AppSecuritySectionProps {
  appId: string
  canManage: boolean
}

interface ScopeSwitchProps {
  description: string
  checked: boolean
  disabled?: boolean
  onChange: (checked: boolean) => void
}

/** 作用域开关行：开关 + 一行作用说明 */
function ScopeSwitch({ description, checked, disabled, onChange }: ScopeSwitchProps) {
  return (
    <Space>
      <Switch size="small" checked={checked} disabled={disabled} onChange={onChange} />
      <Typography.Text type="secondary">{description}</Typography.Text>
    </Space>
  )
}

interface RuleSetEditorProps {
  rules: AppSecurityRuleItem[]
  canManage: boolean
  onChange: (rules: AppSecurityRuleItem[]) => void
}

/** 规则编辑器（内嵌在对应选项卡片内，不单独成卡）：规则行维护 + 空态提示 */
function RuleSetEditor({ rules, canManage, onChange }: RuleSetEditorProps) {
  const { t } = useTranslation()

  const typeOptions = SECURITY_RULE_TYPES.map((value) => ({
    value,
    label: t(`appSecurity.type.${value}`),
  }))

  const strategyOptions = SECURITY_STRATEGIES.map((value) => ({
    value,
    label: t(`appSecurity.strategy.${value}`),
  }))

  const updateRule = (index: number, patch: Partial<AppSecurityRuleItem>) => {
    onChange(rules.map((rule, i) => (i === index ? { ...rule, ...patch } : rule)))
  }

  const addRule = () => {
    onChange([...rules, { name: '', type: 'phone', pattern: '', replacement: '', strategy: 'redact', keepLast: null }])
  }

  const removeRule = (index: number) => {
    onChange(rules.filter((_, i) => i !== index))
  }

  // mask 保留末位仅 custom/银行卡可配，内置手机号/证件/邮箱有固定遮蔽样式
  const keepLastEditable = (rule: AppSecurityRuleItem) => rule.strategy === 'mask' && (rule.type === 'custom' || rule.type === 'bankCard')

  return (
    <Space direction="vertical" size={spacing.sm} style={{ width: '100%' }}>
      <Space style={{ width: '100%', justifyContent: 'space-between' }}>
        <Space>
          {t('appSecurity.rulesTitle')}
          {rules.length > 0 && <Tag>{rules.length}</Tag>}
        </Space>
        {canManage && (
          <Button icon={<PlusOutlined />} onClick={addRule}>
            {t('appSecurity.addRule')}
          </Button>
        )}
      </Space>
      {rules.length === 0 ? (
        <Typography.Text type="secondary">{t('appSecurity.emptyRules')}</Typography.Text>
      ) : (
        rules.map((rule, index) => (
          <Space key={index} wrap align="center" size={spacing.sm}>
            <Input
              style={{ width: 140 }}
              placeholder={t('appSecurity.ruleNamePlaceholder')}
              value={rule.name}
              maxLength={50}
              disabled={!canManage}
              onChange={(e) => updateRule(index, { name: e.target.value })}
            />
            <Select
              style={{ width: 120 }}
              value={rule.type}
              options={typeOptions}
              disabled={!canManage}
              onChange={(value: SecurityRuleType) => updateRule(index, { type: value })}
            />
            <Select
              style={{ width: 104 }}
              value={rule.strategy}
              options={strategyOptions}
              disabled={!canManage}
              onChange={(value: SecurityStrategy) => updateRule(index, { strategy: value })}
            />
            {rule.type === 'custom' && (
              <Input
                style={{ width: 240 }}
                placeholder={t('appSecurity.rulePatternPlaceholder')}
                value={rule.pattern ?? ''}
                maxLength={500}
                disabled={!canManage}
                onChange={(e) => updateRule(index, { pattern: e.target.value })}
              />
            )}
            {keepLastEditable(rule) && (
              <InputNumber
                style={{ width: 110 }}
                min={0}
                max={18}
                value={rule.keepLast ?? 4}
                disabled={!canManage}
                placeholder={t('appSecurity.keepLastPlaceholder')}
                onChange={(value) => updateRule(index, { keepLast: value ?? null })}
              />
            )}
            {rule.strategy === 'redact' && (
              <Input
                style={{ width: 130 }}
                placeholder="***"
                value={rule.replacement ?? ''}
                maxLength={50}
                disabled={!canManage}
                onChange={(e) => updateRule(index, { replacement: e.target.value })}
              />
            )}
            {canManage && (
              <Popconfirm
                title={t('appSecurity.removeConfirm')}
                okText={t('appManage.confirm')}
                cancelText={t('appManage.cancel')}
                onConfirm={() => removeRule(index)}
              >
                <Button icon={<DeleteOutlined />} danger type="text" />
              </Popconfirm>
            )}
          </Space>
        ))
      )}
    </Space>
  )
}

interface SecurityEventsCardProps {
  appId: string
  canManage: boolean
}

const EVENT_PAGE_SIZE = 10

/** 安全事件列表：规则命中审计（按时间倒序，仅管理员可见） */
function SecurityEventsCard({ appId, canManage }: SecurityEventsCardProps) {
  const { t } = useTranslation()
  const [loading, setLoading] = useState(false)
  const [items, setItems] = useState<AppSecurityEventItem[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)

  const load = useCallback(
    async (pageNo: number) => {
      setLoading(true)
      try {
        const res = await getAppSecurityEvents(appId, pageNo, EVENT_PAGE_SIZE)
        setItems(res.items)
        setTotal(res.total)
        setPage(res.pageNo)
      } catch {
        // 错误已由全局请求中间件统一提示
      } finally {
        setLoading(false)
      }
    },
    [appId],
  )

  useEffect(() => {
    if (canManage) {
      void load(1)
    }
  }, [canManage, load])

  if (!canManage) {
    return null
  }

  const columns = [
    { title: t('appSecurity.events.colTime'), dataIndex: 'createTime', render: (v: string) => formatTime(v) },
    { title: t('appSecurity.events.colScope'), dataIndex: 'scope', render: (v: string) => t(`appSecurity.scope.${v}`) },
    {
      title: t('appSecurity.events.colRule'),
      render: (_: unknown, record: AppSecurityEventItem) =>
        record.ruleName || t(`appSecurity.type.${record.ruleType}`),
    },
    {
      title: t('appSecurity.events.colAction'),
      dataIndex: 'action',
      render: (v: string) => t(`appSecurity.action.${v}`),
    },
    { title: t('appSecurity.events.colCount'), dataIndex: 'hitCount', width: 80 },
  ]

  return (
    <DSCard title={t('appSecurity.eventsTitle')}>
      <DataTable<AppSecurityEventItem>
        columns={columns}
        dataSource={items}
        loading={loading}
        rowKey="id"
        onRefresh={() => void load(page)}
        refreshLoading={loading}
        pagination={{
          current: page,
          pageSize: EVENT_PAGE_SIZE,
          total,
          onChange: (p) => void load(p),
        }}
      />
    </DSCard>
  )
}

function formatTime(value: string): string {
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) {
    return value
  }

  const pad = (n: number) => String(n).padStart(2, '0')
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`
}

/**
 * 应用「安全」分区：内容脱敏配置（Agent 应用与流程应用通用）。
 * 每个选项独立成组且常显：内容脱敏卡片内嵌规则编辑器（开启才可维护，关闭不清空已配规则）；
 * 用户输入/工具调用结果/工具调用参数沿用内容规则；模型回复卡片内嵌自己专属的规则编辑器，与内容规则相互独立。
 * 每条规则可选处理策略（redact/mask/hash/block/flag）；安全事件卡片展示规则命中审计。
 * 保存后对对话与运行记录即时生效。
 */
export function AppSecuritySection({ appId, canManage }: AppSecuritySectionProps) {
  const { t } = useTranslation()

  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [enabled, setEnabled] = useState(false)
  const [maskToolResult, setMaskToolResult] = useState(true)
  const [maskToolArgs, setMaskToolArgs] = useState(false)
  const [maskModelOutput, setMaskModelOutput] = useState(false)
  const [maskInput, setMaskInput] = useState(false)
  const [rules, setRules] = useState<AppSecurityRuleItem[]>([])
  const [modelOutputRules, setModelOutputRules] = useState<AppSecurityRuleItem[]>([])

  const load = useCallback(async () => {
    if (!appId) return
    setLoading(true)
    try {
      const config = await getAppSecurity(appId)
      setEnabled(config.enabled)
      setMaskToolResult(config.maskToolResult)
      setMaskToolArgs(config.maskToolArgs)
      setMaskModelOutput(config.maskModelOutput)
      setMaskInput(config.maskInput)
      setRules(config.rules)
      setModelOutputRules(config.modelOutputRules)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [appId])

  useEffect(() => {
    void load()
  }, [load])

  const handleSave = async () => {
    const invalid = [...rules, ...modelOutputRules].some((rule) => rule.type === 'custom' && !rule.pattern?.trim())
    if (invalid) {
      feedback.error(t('appSecurity.patternRequired'))
      return
    }

    setSaving(true)
    try {
      // 两组规则始终随保存原样提交：关闭开关仅隐藏编辑器，不清空已配置规则
      await saveAppSecurity(appId, {
        enabled,
        maskToolResult,
        maskToolArgs,
        maskModelOutput,
        maskInput,
        rules,
        modelOutputRules,
      })
      feedback.success(t('appSecurity.saveSuccess'))
      await load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSaving(false)
    }
  }

  if (loading) {
    return null
  }

  return (
    <Space direction="vertical" size={spacing.md} style={{ width: '100%' }}>
      {!canManage && <Alert type="info" showIcon message={t('appManage.memberHint')} />}

      <DSCard title={t('appSecurity.masterTitle')}>
        <Space direction="vertical" size={spacing.md} style={{ width: '100%' }}>
          <ScopeSwitch
            description={t('appSecurity.enabledDesc')}
            checked={enabled}
            disabled={!canManage}
            onChange={setEnabled}
          />
          {enabled && (
            <>
              <Divider style={{ margin: 0 }} />
              <RuleSetEditor rules={rules} canManage={canManage} onChange={setRules} />
            </>
          )}
        </Space>
      </DSCard>

      <DSCard title={t('appSecurity.scopeToolResult')}>
        <ScopeSwitch
          description={t('appSecurity.scopeToolResultDesc')}
          checked={maskToolResult}
          disabled={!canManage}
          onChange={setMaskToolResult}
        />
      </DSCard>

      <DSCard title={t('appSecurity.scopeToolArgs')}>
        <ScopeSwitch
          description={t('appSecurity.scopeToolArgsDesc')}
          checked={maskToolArgs}
          disabled={!canManage}
          onChange={setMaskToolArgs}
        />
      </DSCard>

      <DSCard title={t('appSecurity.scopeModelOutput')}>
        <Space direction="vertical" size={spacing.md} style={{ width: '100%' }}>
          <ScopeSwitch
            description={t('appSecurity.scopeModelOutputDesc')}
            checked={maskModelOutput}
            disabled={!canManage}
            onChange={setMaskModelOutput}
          />
          {maskModelOutput && (
            <>
              <Divider style={{ margin: 0 }} />
              <RuleSetEditor rules={modelOutputRules} canManage={canManage} onChange={setModelOutputRules} />
            </>
          )}
        </Space>
      </DSCard>

      <DSCard title={t('appSecurity.scopeInput')}>
        <ScopeSwitch
          description={t('appSecurity.scopeInputDesc')}
          checked={maskInput}
          disabled={!canManage || !enabled}
          onChange={setMaskInput}
        />
      </DSCard>

      <SecurityEventsCard appId={appId} canManage={canManage} />

      {canManage && (
        <Button type="primary" loading={saving} onClick={() => void handleSave()}>
          {t('appSecurity.save')}
        </Button>
      )}
    </Space>
  )
}
