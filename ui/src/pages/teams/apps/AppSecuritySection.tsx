import { useCallback, useEffect, useRef, useState } from 'react'
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { Alert, Button, Divider, Form, Input, InputNumber, Popconfirm, Select, Space, Switch, Tag, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import {
  getAppSecurity,
  saveAppSecurity,
  SECURITY_RULE_TYPES,
  SECURITY_STRATEGIES,
  type AppSecurityRuleItem,
  type SecurityRuleType,
  type SecurityStrategy,
} from '@/api/app'

export interface AppSecuritySectionProps {
  appId: string
  canManage: boolean
}

export interface AppSecuritySettingsPanelProps {
  appId: string
  canManage: boolean
  /**
   * 宿主统一保存回调注册（Agent 应用「配置」分区嵌入态使用）：传入后面板不渲染自己的保存按钮、
   * 不再自行提示成功，由宿主页「保存设置」把主配置与脱敏设置一次提交；校验或提交失败向上抛错中断宿主保存。
   * 注册在配置加载完成后才生效，避免宿主在回显前保存到默认值。
   */
  registerSave?: (save: (() => Promise<void>) | null) => void
}

interface RuleSetEditorProps {
  rules: AppSecurityRuleItem[]
  canManage: boolean
  onChange: (rules: AppSecurityRuleItem[]) => void
}

/** 规则编辑器（内嵌在对应开关下，不单独成区）：规则行维护 + 空态提示 */
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
    <Space direction="vertical" size={spacing.sm} style={{ width: '100%', marginBottom: spacing.sm }}>
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

/**
 * 内容脱敏设置面板：总开关 + 各作用域开关 + 各自内嵌的规则编辑器（security 接口保存后即时生效，不经发布快照）。
 * Agent 应用内嵌「配置」分区「内容脱敏」收缩卡片：经 registerSave 把保存回调注册给宿主页，
 * 由页面「保存设置」把主配置与脱敏设置一次提交（面板不渲染保存按钮、不自行提示成功）；
 * 流程应用保持「配置 → 安全」二级页（AppSecuritySection），面板自带保存按钮。
 * 内容脱敏与模型回复各自维护独立规则（开启才显示编辑器，关闭不清空已配规则）；
 * 用户输入/工具调用结果/工具调用参数沿用内容规则；每条规则可选处理策略（redact/mask/hash/block/flag）。
 */
export function AppSecuritySettingsPanel({ appId, canManage, registerSave }: AppSecuritySettingsPanelProps) {
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

  /** 执行保存：自定义正则缺失时提示并抛错（嵌入态中断宿主整体保存；独立态由按钮 catch，全局中间件已提示） */
  const saveRef = useRef<() => Promise<void>>(async () => {})
  saveRef.current = async () => {
    const invalid = [...rules, ...modelOutputRules].some((rule) => rule.type === 'custom' && !rule.pattern?.trim())
    if (invalid) {
      feedback.error(t('appSecurity.patternRequired'))
      throw new Error('custom pattern required')
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
      // 嵌入态由宿主「保存设置」统一提示成功
      if (!registerSave) feedback.success(t('appSecurity.saveSuccess'))
      await load()
    } finally {
      setSaving(false)
    }
  }

  // 嵌入态：配置加载完成后才注册保存回调（加载中不注册，防宿主在回显前把默认值存库）；卸载/换应用时注销
  useEffect(() => {
    if (!registerSave || loading) return
    registerSave(() => saveRef.current())
    return () => registerSave(null)
  }, [registerSave, loading])

  if (loading) {
    return null
  }

  return (
    <div>
      <Form layout="vertical" disabled={!canManage}>
        <Form.Item label={t('appSecurity.masterTitle')} extra={t('appSecurity.enabledDesc')} style={{ marginBottom: spacing.sm }}>
          <Switch checked={enabled} onChange={setEnabled} />
        </Form.Item>
        {enabled && <RuleSetEditor rules={rules} canManage={canManage} onChange={setRules} />}
        <Divider style={{ margin: `${spacing.md}px 0` }} />
        <Form.Item label={t('appSecurity.scopeToolResult')} extra={t('appSecurity.scopeToolResultDesc')} style={{ marginBottom: spacing.sm }}>
          <Switch checked={maskToolResult} onChange={setMaskToolResult} />
        </Form.Item>
        <Form.Item label={t('appSecurity.scopeToolArgs')} extra={t('appSecurity.scopeToolArgsDesc')} style={{ marginBottom: spacing.sm }}>
          <Switch checked={maskToolArgs} onChange={setMaskToolArgs} />
        </Form.Item>
        <Form.Item label={t('appSecurity.scopeModelOutput')} extra={t('appSecurity.scopeModelOutputDesc')} style={{ marginBottom: spacing.sm }}>
          <Switch checked={maskModelOutput} onChange={setMaskModelOutput} />
        </Form.Item>
        {maskModelOutput && <RuleSetEditor rules={modelOutputRules} canManage={canManage} onChange={setModelOutputRules} />}
        <Divider style={{ margin: `${spacing.md}px 0` }} />
        <Form.Item label={t('appSecurity.scopeInput')} extra={t('appSecurity.scopeInputDesc')} style={{ marginBottom: 0 }}>
          {/* 用户输入脱敏受内容脱敏总开关门控（总开关关闭时不可开启） */}
          <Switch checked={maskInput} disabled={!enabled} onChange={setMaskInput} />
        </Form.Item>
      </Form>
      {canManage && !registerSave && (
        <Button
          type="primary"
          style={{ marginTop: spacing.md }}
          loading={saving}
          onClick={() => void saveRef.current().catch(() => {})}
        >
          {t('appSecurity.save')}
        </Button>
      )}
    </div>
  )
}

/**
 * 应用「安全」分区（流程应用「配置 → 安全」二级页）：成员提示 + 内容脱敏设置面板。
 * Agent 应用已不挂独立「安全」菜单，安全设置由「配置」分区「安全」收缩卡片承载（AppSecuritySettingsPanel）；
 * 安全事件审计移至「日志」分区（AppSecurityEventsCard）。
 */
export function AppSecuritySection({ appId, canManage }: AppSecuritySectionProps) {
  const { t } = useTranslation()

  return (
    <Space direction="vertical" size={spacing.md} style={{ width: '100%' }}>
      {!canManage && <Alert type="info" showIcon message={t('appManage.memberHint')} />}
      <AppSecuritySettingsPanel appId={appId} canManage={canManage} />
    </Space>
  )
}
