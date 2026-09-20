import { useCallback, useEffect, useState } from 'react'
import { CheckOutlined, CloseOutlined, SafetyCertificateOutlined, ThunderboltFilled } from '@ant-design/icons'
import { Button, Checkbox, Spin, Tag, Input, Tooltip } from 'antd'
import { useTranslation } from 'react-i18next'
import { feedback } from '@/design-system'
import { getAppUserConfig, saveAppUserConfig, type AppUserSkillOption } from '@/api/app'
import type { ToolApprovalMode } from '@/api/agentChat'
import type { PromptItem } from '@/api/prompt'
import { resolveStorageUrl } from '@/utils/storage'

interface AppUserSettingsProps {
  open: boolean
  appId: string
  experts: PromptItem[]
  /** 当前生效的专家提示词 id（无会话为新会话默认，有会话为会话绑定值），作为面板草稿初始值 */
  currentPromptId: number
  onClose: () => void
  /** 保存成功后回调，携带选中的专家提示词 id 与工具审批模式 */
  onSaved: (promptId: number, approvalMode: ToolApprovalMode) => void
}

/**
 * 应用设置面板（右侧滑出）：用户对该应用的个性化定制，跨会话复用。
 * 专家=提示词（本人个人 + 本团队），保存后作为新会话默认并在会话中即时切换；
 * 技能只能从管理员配置的默认技能目录中勾选/取消（默认全部启用），范围外内容不展示也不可提交；
 * 工具审批模式控制重要工具是否需人工批准。
 */
export function AppUserSettings({ open, appId, experts, currentPromptId, onClose, onSaved }: AppUserSettingsProps) {
  const { t } = useTranslation()
  const [loading, setLoading] = useState(false)
  const [saving, setSaving] = useState(false)
  const [search, setSearch] = useState('')
  const [draftPromptId, setDraftPromptId] = useState(0)
  const [draftSkills, setDraftSkills] = useState<string[]>([])
  const [skillOptions, setSkillOptions] = useState<AppUserSkillOption[]>([])
  const [draftApprovalMode, setDraftApprovalMode] = useState<ToolApprovalMode>('auto')

  useEffect(() => {
    if (!open || !appId) return
    setLoading(true)
    setSearch('')
    // 专家草稿从当前生效值初始化（而非持久化默认），保证面板所见即当前会话实际使用的专家
    setDraftPromptId(currentPromptId)
    getAppUserConfig(appId)
      .then((cfg) => {
        setDraftSkills(cfg.skills)
        setSkillOptions(cfg.defaultSkills)
        setDraftApprovalMode(cfg.toolApprovalMode)
      })
      .catch(() => undefined)
      .finally(() => setLoading(false))
  }, [open, appId, currentPromptId])

  const visibleSkills = useCallback(() => {
    const kw = search.trim().toLowerCase()
    if (!kw) return skillOptions
    return skillOptions.filter(
      (s) => (s.name ?? '').toLowerCase().includes(kw) || (s.key ?? '').toLowerCase().includes(kw),
    )
  }, [search, skillOptions])

  const toggleSkill = useCallback((id: string) => {
    setDraftSkills((prev) => (prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]))
  }, [])

  const handleSave = useCallback(async () => {
    setSaving(true)
    try {
      await saveAppUserConfig(appId, {
        promptId: draftPromptId,
        skills: draftSkills,
        toolApprovalMode: draftApprovalMode,
      })
      feedback.success(t('appChat.settingsSaved'))
      onSaved(draftPromptId, draftApprovalMode)
      onClose()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSaving(false)
    }
  }, [appId, draftApprovalMode, draftPromptId, draftSkills, onClose, onSaved, t])

  const skills = visibleSkills()

  // 关闭时不渲染，避免与专家面板重复挂载同名列表项
  if (!open) return null

  return (
    <aside className={`moai-chat__experts moai-chat__settings${open ? ' is-open' : ''}`}>
      <div className="moai-chat__sidebar-head">
        <span className="moai-chat__sidebar-title">{t('appChat.userSettings')}</span>
        <button type="button" className="moai-chat__experts-close" onClick={onClose} aria-label={t('appChat.close')}>
          <CloseOutlined />
        </button>
      </div>
      <div className="moai-chat__experts-hint">{t('appChat.userSettingsHint')}</div>
      {loading ? (
        <div className="moai-chat__settings-loading">
          <Spin />
        </div>
      ) : (
        <>
          <div className="moai-chat__settings-body">
            <div className="moai-chat__settings-section">{t('appChat.experts')}</div>
            <div className="moai-chat__experts-list">
              {experts.length === 0 && <div className="moai-chat__sessions-empty">{t('appChat.expertsEmpty')}</div>}
              {experts.map((item) => {
                const id = Number(item.promptId ?? 0)
                const active = id === draftPromptId
                const avatar = resolveStorageUrl(item.avatarPath ?? null)
                return (
                  <div
                    key={id}
                    className={`moai-chat__expert${active ? ' is-active' : ''}`}
                    onClick={() => setDraftPromptId(active ? 0 : id)}
                  >
                    <div className="moai-chat__expert-avatar">
                      {avatar ? (
                        <img src={avatar} alt={item.name ?? ''} style={{ width: 30, height: 30, objectFit: 'cover' }} />
                      ) : (
                        <div className="moai-chat__expert-avatar-fallback">{(item.name ?? '?').slice(0, 1).toUpperCase()}</div>
                      )}
                    </div>
                    <div className="moai-chat__expert-body">
                      <div className="moai-chat__expert-name">
                        <span className="moai-chat__expert-name-text">{item.name || t('appChat.untitled')}</span>
                        <Tag className={`moai-chat__expert-source${(item.teamId ?? 0) > 0 ? ' is-team' : ''}`}>
                          {t((item.teamId ?? 0) > 0 ? 'appChat.expertTeam' : 'appChat.expertPersonal')}
                        </Tag>
                      </div>
                      {item.description && <div className="moai-chat__expert-desc">{item.description}</div>}
                    </div>
                    {active && <CheckOutlined className="moai-chat__expert-check" />}
                  </div>
                )
              })}
            </div>

            <div className="moai-chat__settings-section">{t('appChat.skills')}</div>
            <div className="moai-chat__settings-search">
              <Input
                allowClear
                size="small"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder={t('appChat.skillSearchPlaceholder')}
              />
            </div>
            <div className="moai-chat__settings-skills">
              {skills.length === 0 && (
                <div className="moai-chat__sessions-empty">{t('appChat.skillsEmpty')}</div>
              )}
              {skills.map((item) => {
                const id = String(item.id)
                const checked = draftSkills.includes(id)
                return (
                  <div key={id} className="moai-chat__settings-skill" onClick={() => toggleSkill(id)}>
                    <Checkbox checked={checked} onChange={() => toggleSkill(id)} />
                    <span className="moai-chat__settings-skill-name">{item.name || item.key}</span>
                    {!item.isSystem && (item.teamId ?? 0) > 0 && (
                      <Tag className="moai-chat__expert-source is-team">{t('appChat.skillTeam')}</Tag>
                    )}
                  </div>
                )
              })}
            </div>

            <div className="moai-chat__settings-section">{t('appChat.approvalModeLabel')}</div>
            <div className="moai-chat__settings-modes">
              <Tooltip title={t('appChat.modeAutoHint')} placement="top">
                <button
                  type="button"
                  className={`moai-chat__settings-mode${draftApprovalMode === 'auto' ? ' is-active' : ''}`}
                  onClick={() => setDraftApprovalMode('auto')}
                >
                  <ThunderboltFilled />
                  {t('appChat.modeAuto')}
                </button>
              </Tooltip>
              <Tooltip title={t('appChat.modeApprovalHint')} placement="top">
                <button
                  type="button"
                  className={`moai-chat__settings-mode${draftApprovalMode === 'approval' ? ' is-active' : ''}`}
                  onClick={() => setDraftApprovalMode('approval')}
                >
                  <SafetyCertificateOutlined />
                  {t('appChat.modeApproval')}
                </button>
              </Tooltip>
            </div>
          </div>
          <div className="moai-chat__settings-footer">
            <Button type="primary" block loading={saving} onClick={() => void handleSave()}>
              {t('appChat.saveSettings')}
            </Button>
          </div>
        </>
      )}
    </aside>
  )
}
