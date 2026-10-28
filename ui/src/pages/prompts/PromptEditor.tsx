import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { CameraOutlined, CodeOutlined, EyeOutlined } from '@ant-design/icons'
import { Avatar, Button, Card, Form, Input, Segmented, Select, Space, Tooltip, Upload, theme } from 'antd'
import type { UploadProps } from 'antd'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams } from 'react-router'
import { Page, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { VditorMarkdownEditor, type VditorEditorMode } from '@/components/VditorMarkdownEditor'
import { classifyApi, ClassifyType, classifyLabel, type Classify } from '@/api/classify'
import { createPrompt, getPromptDetail, setPromptAvatar, updatePrompt } from '@/api/prompt'
import { resolveStorageUrl, uploadImageWithKey } from '@/utils/storage'

const CONTENT_MAX = 10000

interface PromptEditorFormValues {
  name?: string
  promptClassId?: number
  description?: string
}

/** 浏览器草稿：未保存内容落 localStorage，刷新/重开页面可恢复，保存成功后清除 */
interface PromptDraft {
  name?: string
  promptClassId?: number
  description?: string
  content?: string
  mode?: VditorEditorMode
}

export function PromptEditor() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { token } = theme.useToken()
  const params = useParams<{ promptId?: string; teamId?: string }>()

  /** 团队编辑器：teamId 来自路由；个人编辑器为 0 */
  const teamId = params.teamId ? Number(params.teamId) : 0
  const promptId = params.promptId ? Number(params.promptId) : 0
  const isEdit = promptId > 0

  const [loading, setLoading] = useState(isEdit)
  const [saving, setSaving] = useState(false)
  const [content, setContent] = useState('')
  const [mode, setMode] = useState<VditorEditorMode>('ir')
  const [avatarObjectKey, setAvatarObjectKey] = useState('')
  const [avatarUploading, setAvatarUploading] = useState(false)
  const [classifies, setClassifies] = useState<Classify[]>([])
  const [form] = Form.useForm<PromptEditorFormValues>()
  const nameValue = Form.useWatch('name', form)

  const contentRef = useRef(content)
  contentRef.current = content
  const modeRef = useRef(mode)
  modeRef.current = mode
  const draftKey = isEdit ? `moai-prompt-draft:edit:${promptId}` : `moai-prompt-draft:new:t${teamId}`
  const draftTimer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)
  /** 入队时捕获的待写草稿（卸载 flush 用，不依赖已卸载的表单实例） */
  const pendingDraftRef = useRef<PromptDraft | null>(null)
  /** 恢复提示只弹一次（StrictMode 双调用 effect 时避免重复 toast） */
  const restoreToastShownRef = useRef(false)

  const classOptions = useMemo(
    () => classifies.map((c) => ({ value: Number(c.classifyId), label: classifyLabel(c) })),
    [classifies],
  )

  /** 草稿落 localStorage */
  const writeDraft = useCallback(() => {
    const draft = pendingDraftRef.current
    if (!draft) return
    try {
      localStorage.setItem(draftKey, JSON.stringify(draft))
    } catch {
      // 存储不可用（隐私模式/超限）时静默放弃草稿
    }
  }, [draftKey])

  /** 草稿防抖落 localStorage（表单值变化与编辑器输入共用；值在入队时捕获，定时器触发时表单可能已卸载） */
  const queueDraftSave = useCallback(
    (values?: PromptEditorFormValues) => {
      const v = values ?? form.getFieldsValue()
      pendingDraftRef.current = {
        name: v.name,
        promptClassId: v.promptClassId,
        description: v.description,
        content: contentRef.current,
        mode: modeRef.current,
      }
      clearTimeout(draftTimer.current)
      draftTimer.current = setTimeout(writeDraft, 500)
    },
    [form, writeDraft],
  )

  // 卸载时立即落一次草稿，保留最后几百毫秒的输入
  useEffect(
    () => () => {
      clearTimeout(draftTimer.current)
      writeDraft()
    },
    [writeDraft],
  )

  useEffect(() => {
    classifyApi
      .getClassifies(ClassifyType.Prompt)
      .then(setClassifies)
      .catch(() => {
        // 错误已由全局请求中间件统一提示
      })
  }, [])

  useEffect(() => {
    let cancelled = false
    const applyDetail = () => {
      getPromptDetail(promptId)
        .then((detail) => {
          if (cancelled || !detail) return
          form.setFieldsValue({
            name: detail.name ?? undefined,
            promptClassId: detail.promptClassId || undefined,
            description: detail.description ?? undefined,
          })
          setContent(detail.content ?? '')
          setAvatarObjectKey(detail.avatarPath ?? '')
          // 服务端内容就位后，叠加本地未保存草稿（刷新/误关场景）
          try {
            const raw = localStorage.getItem(draftKey)
            if (raw) {
              const draft = JSON.parse(raw) as PromptDraft
              const hasDraft = Boolean(draft.content?.trim() || draft.name || draft.description)
              if (hasDraft) {
                form.setFieldsValue({
                  name: draft.name || undefined,
                  promptClassId: draft.promptClassId || undefined,
                  description: draft.description || undefined,
                })
                setContent(draft.content ?? '')
                if (draft.mode) setMode(draft.mode)
                if (!restoreToastShownRef.current) {
                  restoreToastShownRef.current = true
                  feedback.info(t('prompt.draftRestored'))
                }
              }
            }
          } catch {
            // 草稿损坏则忽略，展示服务端内容
          }
        })
        .catch(() => {
          // 错误已由全局请求中间件统一提示
        })
        .finally(() => {
          if (!cancelled) setLoading(false)
        })
    }
    if (isEdit) {
      applyDetail()
    } else {
      // 新建页：恢复上次未保存的草稿
      setLoading(false)
      try {
        const raw = localStorage.getItem(draftKey)
        if (raw) {
          const draft = JSON.parse(raw) as PromptDraft
          const hasDraft = Boolean(draft.content?.trim() || draft.name || draft.description)
          if (hasDraft) {
            form.setFieldsValue({
              name: draft.name || undefined,
              promptClassId: draft.promptClassId || undefined,
              description: draft.description || undefined,
            })
            setContent(draft.content ?? '')
            if (draft.mode) setMode(draft.mode)
            if (!restoreToastShownRef.current) {
              restoreToastShownRef.current = true
              feedback.info(t('prompt.draftRestored'))
            }
          }
        }
      } catch {
        // 草稿损坏则忽略
      }
    }
    return () => {
      cancelled = true
    }
  }, [isEdit, promptId, draftKey, form, t])

  const backToList = useCallback(() => {
    navigate(teamId > 0 ? `/team/${teamId}/prompts` : '/prompts')
  }, [navigate, teamId])

  const avatarBeforeUpload: UploadProps['beforeUpload'] = (file) => {
    if (!file.type.startsWith('image/')) {
      feedback.error(t('prompt.avatarTypeError'))
      return Upload.LIST_IGNORE
    }
    if (file.size > 5 * 1024 * 1024) {
      feedback.error(t('prompt.avatarSizeError'))
      return Upload.LIST_IGNORE
    }
    setAvatarUploading(true)
    ;(async () => {
      const { objectKey } = await uploadImageWithKey(file)
      if (isEdit) {
        // 已有提示词：直接落库
        await setPromptAvatar(promptId, objectKey)
      }
      setAvatarObjectKey(objectKey)
      feedback.success(t('prompt.avatarSuccess'))
    })()
      .catch(() => {
        // 错误已由全局请求中间件统一提示
      })
      .finally(() => setAvatarUploading(false))
    return Upload.LIST_IGNORE
  }

  const handleSave = async () => {
    const values = await form.validateFields()
    if (!content.trim()) {
      feedback.error(t('prompt.contentPlaceholder'))
      return
    }
    setSaving(true)
    try {
      if (isEdit) {
        await updatePrompt(promptId, {
          name: values.name!,
          description: values.description,
          content,
          promptClassId: values.promptClassId ?? 0,
          avatarPath: avatarObjectKey || undefined,
        })
        feedback.success(t('prompt.saveSuccess'))
      } else {
        await createPrompt({
          teamId,
          name: values.name!,
          description: values.description,
          content,
          promptClassId: values.promptClassId ?? 0,
          avatarPath: avatarObjectKey || undefined,
        })
        feedback.success(t('prompt.createSuccess'))
      }
      // 先取消未触发的防抖写入，避免已保存内容被重新写回草稿
      clearTimeout(draftTimer.current)
      localStorage.removeItem(draftKey)
      backToList()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSaving(false)
    }
  }

  const listTitle = teamId > 0 ? t('team.prompts') : t('nav.prompts')

  return (
    <Page
      breadcrumb={[
        teamId > 0 ? { title: <Link to="/team">{t('team.title')}</Link> } : { title: <Link to="/prompts">{t('nav.prompts')}</Link> },
        ...(teamId > 0 ? [{ title: <Link to={`/team/${teamId}/prompts`}>{listTitle}</Link> }] : []),
        { title: isEdit ? t('prompt.editorEdit') : t('prompt.editorCreate') },
      ]}
    >
      <Card styles={{ body: { padding: spacing.md, marginBottom: spacing.md } }} loading={loading}>
        <div style={{ display: 'flex', gap: spacing.md, alignItems: 'flex-start', flexWrap: 'wrap' }}>
          <Upload beforeUpload={avatarBeforeUpload} showUploadList={false} accept="image/*">
            <div style={{ position: 'relative', lineHeight: 0, cursor: 'pointer' }} title={t('prompt.avatar')}>
              <Avatar size={64} src={avatarObjectKey ? resolveStorageUrl(avatarObjectKey) : undefined} style={{ opacity: avatarUploading ? 0.55 : 1 }}>
                {(nameValue ?? '?').slice(0, 1).toUpperCase()}
              </Avatar>
              <div
                style={{
                  position: 'absolute',
                  right: -2,
                  bottom: -2,
                  width: 22,
                  height: 22,
                  borderRadius: '50%',
                  background: token.colorPrimary,
                  color: token.colorTextLightSolid,
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  fontSize: 11,
                  border: `2px solid ${token.colorBgContainer}`,
                }}
              >
                <CameraOutlined />
              </div>
            </div>
          </Upload>
          <Form form={form} layout="vertical" style={{ flex: 1, minWidth: 320 }} onValuesChange={(_, all) => queueDraftSave(all)}>
            <div style={{ display: 'flex', gap: spacing.md, flexWrap: 'wrap', alignItems: 'flex-start' }}>
              <Form.Item
                name="name"
                label={t('prompt.name')}
                rules={[{ required: true, message: t('prompt.namePlaceholder') }, { max: 20, message: `${t('prompt.name')} ≤ 20` }]}
                style={{ marginBottom: 0, width: 200 }}
              >
                <Input placeholder={t('prompt.namePlaceholder')} maxLength={20} allowClear />
              </Form.Item>
              <Form.Item name="promptClassId" label={t('prompt.class')} style={{ marginBottom: 0, width: 200 }}>
                <Select allowClear placeholder={t('prompt.classAll')} options={classOptions} />
              </Form.Item>
              {/* 保存/取消与名称同行（底部对齐），避免页头右侧按钮留下大片空白 */}
              <Space style={{ alignSelf: 'flex-end' }}>
                <Button onClick={backToList}>{t('prompt.cancel')}</Button>
                <Button type="primary" loading={saving} onClick={() => void handleSave()}>
                  {isEdit ? t('prompt.save') : t('prompt.create')}
                </Button>
              </Space>
            </div>
            <Form.Item name="description" label={t('prompt.desc')} rules={[{ max: 255 }]} style={{ marginBottom: 0, marginTop: spacing.md }}>
              <Input placeholder={t('prompt.descPlaceholder')} maxLength={255} allowClear />
            </Form.Item>
          </Form>
        </div>
      </Card>
      <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 8 }}>
        <Segmented
          size="small"
          value={mode}
          onChange={(v) => {
            const next = v as VditorEditorMode
            setMode(next)
            queueDraftSave()
          }}
          options={[
            { label: <Tooltip title={t('prompt.modeWysiwyg')}><EyeOutlined aria-label={t('prompt.modeWysiwyg')} /></Tooltip>, value: 'ir' },
            { label: <Tooltip title={t('prompt.modeSource')}><CodeOutlined aria-label={t('prompt.modeSource')} /></Tooltip>, value: 'sv' },
          ]}
        />
      </div>
      {!loading && (
        <VditorMarkdownEditor
          value={content}
          onChange={(v) => {
            contentRef.current = v
            setContent(v)
            queueDraftSave()
          }}
          mode={mode}
          placeholder={t('prompt.editorPlaceholder')}
          maxLength={CONTENT_MAX}
          style={{ height: 'calc(100vh - 242px)', minHeight: 420 }}
        />
      )}
    </Page>
  )
}
