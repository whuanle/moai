import { useCallback, useEffect, useState } from 'react'
import { CheckOutlined, CloseOutlined, LockOutlined } from '@ant-design/icons'
import { Button, Checkbox, Spin, Tag, Input } from 'antd'
import { useTranslation } from 'react-i18next'
import { feedback } from '@/design-system'
import { getAppUserConfig, saveAppUserConfig } from '@/api/app'
import { getSkillOptions, type SkillOption } from '@/api/skills'
import type { PromptItem } from '@/api/prompt'
import { resolveStorageUrl } from '@/utils/storage'

interface AppUserSettingsProps {
  open: boolean
  appId: string
  teamId: number
  experts: PromptItem[]
  onClose: () => void
  /** 保存成功后回调，携带新会话默认专家提示词 id */
  onSaved: (promptId: number) => void
}

/**
 * 应用设置面板（右侧滑出）：用户对该应用的个性化定制，跨会话复用。
 * 专家=新会话默认提示词；技能=自选技能与应用绑定技能（锁定）取并集生效。
 */
export function AppUserSettings({ open, appId, teamId, experts, onClose, onSaved }: AppUserSettingsProps) {
  const { t } = useTranslation()
  const [loading, setLoading] = useState(false)
  const [saving, setSaving] = useState(false)
  const [search, setSearch] = useState('')
  const [draftPromptId, setDraftPromptId] = useState(0)
  const [draftSkills, setDraftSkills] = useState<string[]>([])
  const [lockedSkills, setLockedSkills] = useState<SkillOption[]>([])
  const [skillOptions, setSkillOptions] = useState<SkillOption[]>([])

  useEffect(() => {
    if (!open || !appId) return
    setLoading(true)
    Promise.all([
      getAppUserConfig(appId),
      getSkillOptions({ teamId, includePersonal: true }).catch(() => [] as SkillOption[]),
    ])
      .then(([cfg, options]) => {
        setDraftPromptId(cfg.promptId)
        setDraftSkills(cfg.skills)
        const lockedIds = new Set(cfg.lockedSkills)
        setLockedSkills(options.filter((o) => lockedIds.has(String(o.id))))
        setSkillOptions(options.filter((o) => !lockedIds.has(String(o.id))))
      })
      .catch(() => undefined)
      .finally(() => setLoading(false))
  }, [open, appId, teamId])

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
      await saveAppUserConfig(appId, { promptId: draftPromptId, skills: draftSkills })
      feedback.success(t('appChat.settingsSaved'))
      onSaved(draftPromptId)
      onClose()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSaving(false)
    }
  }, [appId, draftPromptId, draftSkills, onClose, onSaved, t])

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
            <div className="moai-chat__settings-section">{t('appChat.defaultExpert')}</div>
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
              {lockedSkills.length === 0 && skills.length === 0 && (
                <div className="moai-chat__sessions-empty">{t('appChat.skillsEmpty')}</div>
              )}
              {lockedSkills.map((item) => (
                <div key={String(item.id)} className="moai-chat__settings-skill is-locked">
                  <Checkbox checked disabled />
                  <span className="moai-chat__settings-skill-name">{item.name || item.key}</span>
                  <Tag className="moai-chat__settings-skill-lock">
                    <LockOutlined /> {t('appChat.skillLocked')}
                  </Tag>
                </div>
              ))}
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
                    {!item.isSystem && (item.teamId ?? 0) === 0 && (
                      <Tag className="moai-chat__expert-source">{t('appChat.skillPersonal')}</Tag>
                    )}
                  </div>
                )
              })}
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
