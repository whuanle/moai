import { useEffect, useState, type ReactNode } from 'react'
import {
  Button,
  Collapse,
  Drawer,
  Input,
  InputNumber,
  Popconfirm,
  Select,
  Space,
  Switch,
  Tag,
  Typography,
} from 'antd'
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import {
  pluginApi,
  type PluginSemanticData,
  type PluginSemanticNoteItem,
  type PluginToolItem,
  type PluginToolMetaItem,
} from '@/api/plugin'
import { getTeamPluginSemantic, saveTeamPluginSemantic } from '@/api/team-plugin'
import { feedback } from '@/design-system'

/** 单个工具的可编辑语义配置（调用策略拆为规则行与动作，保存时再合成 JSON）. */
interface ToolSemanticDraft {
  extraNotes: string
  denyPatternsText: string
  action: 'block' | 'confirm' | 'audit'
  readOnlyHint: boolean
  destructiveHint: boolean
}

/** 解析调用策略 JSON 为可编辑草稿；非法 JSON 静默回落空配置. */
function parseCallPolicy(callPolicy: string): { denyPatternsText: string; action: ToolSemanticDraft['action'] } {
  if (!callPolicy.trim()) return { denyPatternsText: '', action: 'block' }
  try {
    const parsed = JSON.parse(callPolicy) as { denyPatterns?: string[]; action?: string }
    const action = parsed.action === 'confirm' || parsed.action === 'audit' ? parsed.action : 'block'
    return { denyPatternsText: (parsed.denyPatterns ?? []).join('\n'), action }
  } catch {
    return { denyPatternsText: '', action: 'block' }
  }
}

/** 合成调用策略 JSON；无规则返回空串（未配置）. */
function buildCallPolicy(draft: Pick<ToolSemanticDraft, 'denyPatternsText' | 'action'>): string {
  const denyPatterns = draft.denyPatternsText
    .split('\n')
    .map((x) => x.trim())
    .filter(Boolean)
  if (denyPatterns.length === 0) return ''
  return JSON.stringify({ denyPatterns, action: draft.action })
}

/** 插件语义配置抽屉：使用须知 + 工具级补充说明/调用策略 + 语义条目（整体覆盖保存）；传 teamId 走团队自有插件端点. */
export function PluginSemanticModal({
  open,
  pluginId,
  pluginTitle,
  teamId,
  onClose,
  onSaved,
}: {
  open: boolean
  pluginId?: string | null
  pluginTitle?: string | null
  /** 团队场景传入：语义读写走 /team/{teamId}/plugin/{id}/semantic（团队自有插件）. */
  teamId?: number
  onClose: () => void
  onSaved: () => void
}) {
  const { t } = useTranslation()
  const [loading, setLoading] = useState(false)
  const [saving, setSaving] = useState(false)
  const [usageNotes, setUsageNotes] = useState('')
  const [tools, setTools] = useState<PluginToolItem[]>([])
  const [toolDrafts, setToolDrafts] = useState<Record<string, ToolSemanticDraft>>({})
  const [notes, setNotes] = useState<PluginSemanticNoteItem[]>([])

  useEffect(() => {
    if (!open || !pluginId) return
    setLoading(true)
    const load = teamId != null ? getTeamPluginSemantic(teamId, pluginId) : pluginApi.getPluginSemantic(pluginId)
    load
      .then((data: PluginSemanticData | null) => {
        if (!data) return
        setUsageNotes(data.usageNotes)
        setTools(data.tools)
        const drafts: Record<string, ToolSemanticDraft> = {}
        for (const tool of data.tools) {
          const meta: PluginToolMetaItem | undefined = data.toolMetas.find((m) => m.toolKey === tool.key)
          const policy = parseCallPolicy(meta?.callPolicy ?? '')
          drafts[tool.key] = {
            extraNotes: meta?.extraNotes ?? '',
            denyPatternsText: policy.denyPatternsText,
            action: policy.action,
            readOnlyHint: meta?.readOnlyHint ?? false,
            destructiveHint: meta?.destructiveHint ?? false,
          }
        }

        setToolDrafts(drafts)
        setNotes(data.notes)
      })
      .catch(() => {
        // 错误已由全局请求中间件统一提示
      })
      .finally(() => setLoading(false))
  }, [open, pluginId, teamId])

  const updateDraft = (toolKey: string, patch: Partial<ToolSemanticDraft>) => {
    setToolDrafts((prev) => ({ ...prev, [toolKey]: { ...prev[toolKey], ...patch } }))
  }

  const updateNote = (index: number, patch: Partial<PluginSemanticNoteItem>) => {
    setNotes((prev) => prev.map((item, i) => (i === index ? { ...item, ...patch } : item)))
  }

  const addNote = () => {
    setNotes((prev) => [
      ...prev,
      { id: null, toolKey: '', title: '', content: '', mode: 'onDemand', sortOrder: prev.length },
    ])
  }

  const handleSave = async () => {
    if (!pluginId) return
    setSaving(true)
    try {
      const toolMetas = tools
        .map((tool) => {
          const draft = toolDrafts[tool.key]
          if (!draft) return null
          return {
            toolKey: tool.key,
            extraNotes: draft.extraNotes,
            callPolicy: buildCallPolicy(draft),
            readOnlyHint: draft.readOnlyHint,
            destructiveHint: draft.destructiveHint,
          }
        })
        .filter((x): x is NonNullable<typeof x> => x !== null)
      const save =
        teamId != null
          ? saveTeamPluginSemantic(teamId, pluginId, { usageNotes, notes, toolMetas })
          : pluginApi.savePluginSemantic(pluginId, { usageNotes, notes, toolMetas })
      await save
      feedback.success(t('plugins.semanticSaveSuccess'))
      onSaved()
      onClose()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSaving(false)
    }
  }

  const toolOptions = [
    { value: '', label: t('plugins.semanticNoteToolLevel') },
    ...tools.map((x) => ({ value: x.key, label: x.title ? `${x.title} (${x.key})` : x.key })),
  ]

  return (
    <Drawer
      open={open}
      onClose={onClose}
      width="52%"
      maskClosable={false}
      title={`${t('plugins.semanticConfig')}${pluginTitle ? ` · ${pluginTitle}` : ''}`}
      styles={{ body: { padding: 16, display: 'flex', flexDirection: 'column', height: '100%' } }}
      footer={
        <Space style={{ float: 'right' }}>
          <Button onClick={onClose}>{t('plugins.close')}</Button>
          <Button type="primary" loading={saving} onClick={() => void handleSave()}>
            {t('plugins.save')}
          </Button>
        </Space>
      }
      footerStyle={{ textAlign: 'right' }}
    >
      <div style={{ flex: 1, minHeight: 0, overflowY: 'auto' }}>
        <Typography.Title level={5} style={{ marginTop: 0 }}>
          {t('plugins.semanticUsageNotes')}
        </Typography.Title>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
          {t('plugins.semanticUsageNotesTip')}
        </Typography.Paragraph>
        <Input.TextArea
          rows={4}
          maxLength={2000}
          showCount
          value={usageNotes}
          onChange={(e) => setUsageNotes(e.target.value)}
          placeholder={t('plugins.semanticUsageNotesPlaceholder')}
          disabled={loading}
        />

        <Typography.Title level={5}>{t('plugins.semanticTools')}</Typography.Title>
        <Collapse
          className="mb-4"
          items={tools.map((tool) => {
            const draft = toolDrafts[tool.key]
            const hints: ReactNode[] = []
            if (draft?.readOnlyHint) hints.push(<Tag key="ro" color="blue">{t('plugins.semanticReadOnlyHint')}</Tag>)
            if (draft?.destructiveHint) hints.push(<Tag key="de" color="orange">{t('plugins.semanticDestructiveHint')}</Tag>)
            return {
              key: tool.key,
              label: (
                <Space size={8} wrap>
                  <span>{tool.title || tool.key}</span>
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    {tool.key}
                  </Typography.Text>
                  {hints}
                </Space>
              ),
              children: (
                <div>
                  <Typography.Paragraph type="secondary" style={{ marginBottom: 4, fontSize: 12 }}>
                    {tool.description}
                  </Typography.Paragraph>
                  <div style={{ marginBottom: 12 }}>
                    <div style={{ marginBottom: 4 }}>{t('plugins.semanticExtraNotes')}</div>
                    <Input.TextArea
                      rows={2}
                      maxLength={2000}
                      showCount
                      value={draft?.extraNotes ?? ''}
                      onChange={(e) => updateDraft(tool.key, { extraNotes: e.target.value })}
                      disabled={loading}
                    />
                  </div>
                  <div style={{ marginBottom: 12 }}>
                    <div style={{ marginBottom: 4 }}>{t('plugins.semanticDenyPatterns')}</div>
                    <Input.TextArea
                      rows={3}
                      value={draft?.denyPatternsText ?? ''}
                      onChange={(e) => updateDraft(tool.key, { denyPatternsText: e.target.value })}
                      placeholder="^drop\\s|^delete\\s|^truncate\\s"
                      disabled={loading}
                    />
                  </div>
                  <Space size={24} wrap>
                    <Space size={8}>
                      <span>{t('plugins.semanticAction')}</span>
                      <Select
                        style={{ width: 160 }}
                        value={draft?.action ?? 'block'}
                        onChange={(v) => updateDraft(tool.key, { action: v })}
                        options={[
                          { value: 'block', label: t('plugins.semanticActionBlock') },
                          { value: 'confirm', label: t('plugins.semanticActionConfirm') },
                          { value: 'audit', label: t('plugins.semanticActionAudit') },
                        ]}
                        disabled={loading}
                      />
                    </Space>
                    <Space size={8}>
                      <span>{t('plugins.semanticReadOnlyHint')}</span>
                      <Switch
                        checked={draft?.readOnlyHint ?? false}
                        onChange={(v) => updateDraft(tool.key, { readOnlyHint: v })}
                        disabled={loading}
                      />
                    </Space>
                    <Space size={8}>
                      <span>{t('plugins.semanticDestructiveHint')}</span>
                      <Switch
                        checked={draft?.destructiveHint ?? false}
                        onChange={(v) => updateDraft(tool.key, { destructiveHint: v })}
                        disabled={loading}
                      />
                    </Space>
                  </Space>
                </div>
              ),
            }
          })}
        />

        <Typography.Title level={5}>{t('plugins.semanticNotes')}</Typography.Title>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 8 }}>
          {t('plugins.semanticNotesTip')}
        </Typography.Paragraph>
        {notes.map((note, index) => (
          <div
            key={note.id ?? `new-${index}`}
            style={{ border: '1px solid rgba(128,128,128,0.25)', borderRadius: 8, padding: 12, marginBottom: 12 }}
          >
            <Space size={8} wrap style={{ marginBottom: 8 }}>
              <Select
                style={{ width: 220 }}
                value={note.toolKey}
                onChange={(v) => updateNote(index, { toolKey: v })}
                options={toolOptions}
                disabled={loading}
              />
              <Input
                style={{ width: 220 }}
                maxLength={100}
                placeholder={t('plugins.semanticNoteTitle')}
                value={note.title}
                onChange={(e) => updateNote(index, { title: e.target.value })}
                disabled={loading}
              />
              <Select
                style={{ width: 130 }}
                value={note.mode}
                onChange={(v) => updateNote(index, { mode: v })}
                options={[
                  { value: 'always', label: t('plugins.semanticModeAlways') },
                  { value: 'onDemand', label: t('plugins.semanticModeOnDemand') },
                ]}
                disabled={loading}
              />
              <InputNumber
                min={0}
                value={note.sortOrder}
                onChange={(v) => updateNote(index, { sortOrder: Number(v ?? 0) })}
                disabled={loading}
              />
              <Popconfirm title={t('plugins.semanticNoteDeleteConfirm')} okButtonProps={{ danger: true }} onConfirm={() => setNotes((prev) => prev.filter((_, i) => i !== index))}>
                <Button type="link" size="small" danger icon={<DeleteOutlined />}>
                  {t('plugins.delete')}
                </Button>
              </Popconfirm>
            </Space>
            <Input.TextArea
              rows={4}
              maxLength={note.mode === 'always' ? 4000 : 12000}
              showCount
              value={note.content}
              onChange={(e) => updateNote(index, { content: e.target.value })}
              placeholder={t('plugins.semanticNoteContentPlaceholder')}
              disabled={loading}
            />
          </div>
        ))}
        <Button type="dashed" block icon={<PlusOutlined />} onClick={addNote} disabled={loading}>
          {t('plugins.semanticNoteAdd')}
        </Button>
      </div>
    </Drawer>
  )
}
