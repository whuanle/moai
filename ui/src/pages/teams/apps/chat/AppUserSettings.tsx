import { useCallback, useEffect, useState } from 'react'
import { CheckOutlined, CloseOutlined, SafetyCertificateOutlined, ThunderboltFilled } from '@ant-design/icons'
import { Button, Input, Spin, Tag, Tooltip } from 'antd'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { feedback } from '@/design-system'
import { getAppUserConfig, saveAppUserConfig, type AppUserSkillOption } from '@/api/app'
import type { ToolApprovalMode } from '@/api/agentChat'
import type { PromptItem } from '@/api/prompt'
import { resolveStorageUrl } from '@/utils/storage'

/** 用户记忆模式（对齐后端 AgentMemorySettings.UserMode*）：仅 开启（2）/ 关闭（1）两态 */
const MEMORY_MODE_OFF = 1
const MEMORY_MODE_APP_ONLY = 2

/** 面板宽度持久化键与取值范围 */
const PANEL_WIDTH_KEY = 'moai:chat:settings-width'
const DEFAULT_PANEL_WIDTH = 460
const MIN_PANEL_WIDTH = 360
const clampPanelWidth = (width: number) => {
  const max = Math.max(MIN_PANEL_WIDTH, Math.min(760, Math.round(window.innerWidth * 0.7)))
  return Math.min(Math.max(Math.round(width) || DEFAULT_PANEL_WIDTH, MIN_PANEL_WIDTH), max)
}

interface AppUserSettingsProps {
  open: boolean
  /** 团队 id（「管理记忆」跳转用） */
  teamId: number
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
 * 工具审批模式控制重要工具是否需人工批准；
 * 长期记忆偏好（应用开启记忆时展示）：模式选择 + 本人记忆管理（单条删除/清空本应用/清空全部）。
 */
export function AppUserSettings({ open, teamId, appId, experts, currentPromptId, onClose, onSaved }: AppUserSettingsProps) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [loading, setLoading] = useState(false)
  const [saving, setSaving] = useState(false)
  const [search, setSearch] = useState('')
  const [expertSearch, setExpertSearch] = useState('')
  const [draftPromptId, setDraftPromptId] = useState(0)
  const [draftSkills, setDraftSkills] = useState<string[]>([])
  const [skillOptions, setSkillOptions] = useState<AppUserSkillOption[]>([])
  const [draftApprovalMode, setDraftApprovalMode] = useState<ToolApprovalMode>('auto')
  // 长期记忆偏好（仅 开启/关闭 两态；明细管理在「管理记忆」页）
  const [memoryEnabled, setMemoryEnabled] = useState(false)
  const [draftMemoryMode, setDraftMemoryMode] = useState(MEMORY_MODE_APP_ONLY)
  // 面板宽度：左缘手柄拖拽调整，localStorage 持久化
  const [panelWidth, setPanelWidth] = useState(() => {
    const saved = Number(localStorage.getItem(PANEL_WIDTH_KEY))
    return clampPanelWidth(saved > 0 ? saved : DEFAULT_PANEL_WIDTH)
  })
  const [resizing, setResizing] = useState(false)

  const startResize = useCallback((e: React.PointerEvent) => {
    e.preventDefault()
    setResizing(true)
    document.body.classList.add('moai-chat-resizing')
    const onMove = (ev: PointerEvent) => {
      const width = clampPanelWidth(window.innerWidth - ev.clientX)
      setPanelWidth(width)
    }
    const onUp = (ev: PointerEvent) => {
      window.removeEventListener('pointermove', onMove)
      window.removeEventListener('pointerup', onUp)
      document.body.classList.remove('moai-chat-resizing')
      setResizing(false)
      const width = clampPanelWidth(window.innerWidth - ev.clientX)
      setPanelWidth(width)
      try {
        localStorage.setItem(PANEL_WIDTH_KEY, String(width))
      } catch {
        // 隐私模式等存储不可用时忽略
      }
    }
    window.addEventListener('pointermove', onMove)
    window.addEventListener('pointerup', onUp)
  }, [])

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
        setMemoryEnabled(cfg.memoryEnabled)
        // 未设置（0）按开启生效；面板只呈现 开启/关闭 两态
        setDraftMemoryMode(cfg.memoryMode === 1 ? MEMORY_MODE_OFF : MEMORY_MODE_APP_ONLY)
      })
      .catch(() => undefined)
      .finally(() => setLoading(false))
  }, [open, appId, currentPromptId])

  /** 卡片列表通用规则：关键词命中（名称优先）→ 热度/目录序 → 截断 10 条；已选项不在列表时置顶 */
  const pickTopCards = useCallback(
    <T,>(pool: T[], keyword: string, hits: (item: T) => boolean, nameHit: (item: T) => boolean, hotter: (a: T, b: T) => number, isActive: (item: T) => boolean): T[] => {
      const kw = keyword.trim().toLowerCase()
      let list = pool
      if (kw) {
        list = pool
          .map((item) => ({ item, rank: (nameHit(item) ? 2 : 0) + (hits(item) ? 1 : 0) }))
          .filter((x) => x.rank > 0)
          .sort((a, b) => b.rank - a.rank || hotter(a.item, b.item))
          .map((x) => x.item)
      } else {
        list = [...pool].sort(hotter)
      }
      const top = list.slice(0, 10)
      // 无关键词时把已选项置顶（避免用户找不到当前选择）；搜索态只展示命中项
      const active = kw ? undefined : pool.find(isActive)
      if (active && !top.some(isActive)) {
        return [active, ...top.slice(0, 9)]
      }
      return top
    },
    [],
  )

  /** 专家卡片：关键词命中名称/描述，热门（useCount）优先，默认展示最热门十个 */
  const visibleExperts = useCallback(() => {
    const seen = new Set<number>()
    const pool = experts.filter((e) => {
      const id = Number(e.promptId ?? 0)
      if (!id || seen.has(id)) return false
      seen.add(id)
      return true
    })
    return pickTopCards(
      pool,
      expertSearch,
      (e) => `${e.name ?? ''}${e.description ?? ''}`.toLowerCase().includes(expertSearch.trim().toLowerCase()),
      (e) => (e.name ?? '').toLowerCase().includes(expertSearch.trim().toLowerCase()),
      (a, b) => (b.useCount ?? 0) - (a.useCount ?? 0),
      (e) => Number(e.promptId ?? 0) === draftPromptId,
    )
  }, [experts, expertSearch, draftPromptId, pickTopCards])

  /** 技能卡片：关键词命中名称/key/描述，热门（useCount，装载工具被模型调用次数）优先；已勾选而未入选的置顶 */
  const visibleSkills = useCallback(() => {
    return pickTopCards(
      skillOptions,
      search,
      (s) => `${s.name ?? ''}${s.key ?? ''}${s.description ?? ''}`.toLowerCase().includes(search.trim().toLowerCase()),
      (s) => (s.name ?? '').toLowerCase().includes(search.trim().toLowerCase()),
      (a, b) => (b.useCount ?? 0) - (a.useCount ?? 0),
      (s) => draftSkills.includes(String(s.id)),
    )
  }, [skillOptions, search, draftSkills, pickTopCards])

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
        ...(memoryEnabled ? { memoryMode: draftMemoryMode } : {}),
      })
      feedback.success(t('appChat.settingsSaved'))
      onSaved(draftPromptId, draftApprovalMode)
      onClose()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSaving(false)
    }
  }, [appId, draftApprovalMode, draftMemoryMode, draftPromptId, draftSkills, memoryEnabled, onClose, onSaved, t])

  const skills = visibleSkills()

  const memoryModeOptions = [
    { value: MEMORY_MODE_APP_ONLY, label: t('appChat.memoryModeOn'), hint: t('appChat.memoryModeOnHint') },
    { value: MEMORY_MODE_OFF, label: t('appChat.memoryModeOff'), hint: t('appChat.memoryModeOffHint') },
  ]

  // 关闭时不渲染，避免与专家面板重复挂载同名列表项
  if (!open) return null

  return (
    <aside
      className={`moai-chat__experts moai-chat__settings${open ? ' is-open' : ''}${resizing ? ' is-resizing' : ''}`}
      style={open ? { width: panelWidth } : undefined}
    >
      <div
        className="moai-chat__settings-resizer"
        role="separator"
        aria-orientation="vertical"
        aria-label={t('appChat.resizePanel')}
        onPointerDown={startResize}
      />
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
            <div className="moai-chat__settings-search">
              <Input
                allowClear
                size="small"
                value={expertSearch}
                onChange={(e) => setExpertSearch(e.target.value)}
                placeholder={t('appChat.expertSearchPlaceholder')}
              />
            </div>
            <div className="moai-chat__experts-list">
              {(() => {
                const cards = visibleExperts()
                if (cards.length === 0) return <div className="moai-chat__sessions-empty">{t('appChat.expertsEmpty')}</div>
                return (
                  <div className="moai-chat__opt-grid">
                    {cards.map((item) => {
                      const id = Number(item.promptId ?? 0)
                      const active = id === draftPromptId
                      const avatar = resolveStorageUrl(item.avatarPath ?? null)
                      return (
                        <div
                          key={id}
                          className={`moai-chat__opt-card${active ? ' is-active' : ''}`}
                          onClick={() => setDraftPromptId(active ? 0 : id)}
                        >
                          <div className="moai-chat__opt-card-head">
                            <div className="moai-chat__opt-card-avatar">
                              {avatar ? (
                                <img src={avatar} alt={item.name ?? ''} />
                              ) : (
                                <div className="moai-chat__opt-card-avatar-fallback">{(item.name ?? '?').slice(0, 1).toUpperCase()}</div>
                              )}
                            </div>
                            <span className="moai-chat__opt-card-name" title={item.name ?? ''}>
                              {item.name || t('appChat.untitled')}
                            </span>
                            <Tag className={`moai-chat__expert-source${(item.teamId ?? 0) > 0 ? ' is-team' : ''}`}>
                              {t((item.teamId ?? 0) > 0 ? 'appChat.expertTeam' : 'appChat.expertPersonal')}
                            </Tag>
                            {active && <CheckOutlined className="moai-chat__opt-card-check" />}
                          </div>
                          {item.description && (
                            <div className="moai-chat__opt-card-desc" title={item.description}>
                              {item.description}
                            </div>
                          )}
                        </div>
                      )
                    })}
                  </div>
                )
              })()}
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
              <div className="moai-chat__opt-grid">
                {skills.map((item) => {
                  const id = String(item.id)
                  const checked = draftSkills.includes(id)
                  return (
                    <div
                      key={id}
                      className={`moai-chat__opt-card${checked ? ' is-active' : ''}`}
                      onClick={() => toggleSkill(id)}
                    >
                      <div className="moai-chat__opt-card-head">
                        <div className="moai-chat__opt-card-avatar">
                          <div className="moai-chat__opt-card-avatar-fallback">{(item.name || item.key || '?').slice(0, 1)}</div>
                        </div>
                        <span className="moai-chat__opt-card-name" title={item.name || item.key || ''}>
                          {item.name || item.key}
                        </span>
                        {!item.isSystem && (item.teamId ?? 0) > 0 && (
                          <Tag className="moai-chat__expert-source is-team">{t('appChat.skillTeam')}</Tag>
                        )}
                        {checked && <CheckOutlined className="moai-chat__opt-card-check" />}
                      </div>
                      {item.description && (
                        <div className="moai-chat__opt-card-desc" title={item.description}>
                          {item.description}
                        </div>
                      )}
                    </div>
                  )
                })}
              </div>
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

            {memoryEnabled && (
              <>
                <div className="moai-chat__settings-section">{t('appChat.memorySection')}</div>
                <div className="moai-chat__settings-modes">
                  {memoryModeOptions.map((option) => (
                    <Tooltip key={option.value} title={option.hint} placement="top">
                      <button
                        type="button"
                        className={`moai-chat__settings-mode${draftMemoryMode === option.value ? ' is-active' : ''}`}
                        onClick={() => setDraftMemoryMode(option.value)}
                      >
                        {option.label}
                      </button>
                    </Tooltip>
                  ))}
                </div>
                <div style={{ padding: '4px 12px 12px' }}>
                  <Button
                    size="small"
                    block
                    onClick={() => {
                      onClose()
                      navigate(`/team/${teamId}/app/${appId}/memory/mine`)
                    }}
                  >
                    {t('myMemory.manage')}
                  </Button>
                </div>
              </>
            )}
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
