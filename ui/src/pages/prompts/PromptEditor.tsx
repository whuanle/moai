import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { CameraOutlined } from '@ant-design/icons'
import { Avatar, Button, Card, Form, Input, Select, Space, Upload, Typography, theme } from 'antd'
import type { UploadProps } from 'antd'
import type { TextAreaRef } from 'antd/es/input/TextArea'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams } from 'react-router'
import { Page, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { classifyApi, ClassifyType, type Classify } from '@/api/classify'
import { createPrompt, getPromptDetail, setPromptAvatar, updatePrompt } from '@/api/prompt'
import { resolveStorageUrl, uploadImageWithKey } from '@/utils/storage'
import { ReactMarkdownPreview } from './PromptDetailModal'
import { MarkdownToolbar } from './MarkdownToolbar'
import { applyMarkdownEdit, type MarkdownAction, type MarkdownStrings } from './markdown'

const { Text } = Typography

const CONTENT_MAX = 10000

interface PromptEditorFormValues {
  name?: string
  promptClassId?: number
  description?: string
}

/**
 * 提示词编辑器独立页：顶部基本信息，下方 Markdown 工具栏 + 左编辑右实时预览，支持上传头像。
 * 路由：/prompts/new、/prompts/:promptId/edit（个人）；/team/:teamId/prompt/new、/team/:teamId/prompt/:promptId/edit（团队）。
 */
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
  const [avatarObjectKey, setAvatarObjectKey] = useState('')
  const [avatarUploading, setAvatarUploading] = useState(false)
  const [classifies, setClassifies] = useState<Classify[]>([])
  const [form] = Form.useForm<PromptEditorFormValues>()
  const nameValue = Form.useWatch('name', form)
  const textareaRef = useRef<TextAreaRef>(null)

  const classOptions = useMemo(
    () => classifies.map((c) => ({ value: Number(c.classifyId), label: c.name ?? '' })),
    [classifies],
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
    if (!isEdit) return
    let cancelled = false
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
      })
      .catch(() => {
        // 错误已由全局请求中间件统一提示
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [isEdit, promptId, form])

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

  const toolStrings = useMemo<MarkdownStrings>(
    () => ({
      text: t('prompt.toolText'),
      code: t('prompt.toolCode'),
      linkText: t('prompt.toolLinkText'),
      linkUrl: 'https://',
      tableHeader: t('prompt.toolTableHeader'),
      tableCell: t('prompt.toolTableCell'),
    }),
    [t],
  )

  const handleToolbar = (action: MarkdownAction) => {
    // antd TextArea 的 ref 是 TextAreaRef，原生元素在 resizableTextArea.textArea
    const textarea = textareaRef.current?.resizableTextArea?.textArea
    if (!textarea) return
    const result = applyMarkdownEdit(
      textarea.value,
      { start: textarea.selectionStart, end: textarea.selectionEnd },
      action,
      toolStrings,
    )
    // 手动插入不受 maxLength 限制，这里与输入框上限保持一致
    const next = result.content.slice(0, CONTENT_MAX)
    setContent(next)
    requestAnimationFrame(() => {
      textarea.focus()
      textarea.setSelectionRange(Math.min(result.selectionStart, next.length), Math.min(result.selectionEnd, next.length))
    })
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
      backToList()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSaving(false)
    }
  }

  const listTitle = teamId > 0 ? t('team.prompts') : t('nav.prompts')
  const paneStyle = {
    display: 'flex',
    flexDirection: 'column' as const,
    minWidth: 0,
    border: `1px solid ${token.colorBorder}`,
    borderRadius: token.borderRadiusLG,
    overflow: 'hidden' as const,
    background: token.colorBgContainer,
  }
  const paneHeaderStyle = {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: spacing.sm,
    padding: '6px 12px',
    borderBottom: `1px solid ${token.colorBorder}`,
    background: token.colorFillQuaternary,
    flexShrink: 0,
  }

  return (
    <Page
      breadcrumb={[
        teamId > 0 ? { title: <Link to="/team">{t('team.title')}</Link> } : { title: <Link to="/prompts">{t('nav.prompts')}</Link> },
        ...(teamId > 0 ? [{ title: <Link to={`/team/${teamId}/prompts`}>{listTitle}</Link> }] : []),
        { title: isEdit ? t('prompt.editorEdit') : t('prompt.editorCreate') },
      ]}
      extra={
        <Space>
          <Button onClick={backToList}>{t('prompt.cancel')}</Button>
          <Button type="primary" loading={saving} onClick={() => void handleSave()}>
            {isEdit ? t('prompt.save') : t('prompt.create')}
          </Button>
        </Space>
      }
    >
      <Card styles={{ body: { padding: spacing.md, marginBottom: spacing.md } }} loading={loading}>
        <div style={{ display: 'flex', gap: spacing.md, alignItems: 'center', flexWrap: 'wrap' }}>
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
          <Form form={form} layout="vertical" style={{ flex: 1, minWidth: 320 }}>
            <div style={{ display: 'flex', gap: spacing.md, flexWrap: 'wrap', alignItems: 'flex-start' }}>
              <Form.Item
                name="name"
                label={t('prompt.name')}
                rules={[{ required: true, message: t('prompt.namePlaceholder') }, { max: 20, message: `${t('prompt.name')} ≤ 20` }]}
                style={{ marginBottom: 0, width: 240 }}
              >
                <Input placeholder={t('prompt.namePlaceholder')} maxLength={20} allowClear />
              </Form.Item>
              <Form.Item name="promptClassId" label={t('prompt.class')} style={{ marginBottom: 0, width: 200 }}>
                <Select allowClear placeholder={t('prompt.classAll')} options={classOptions} />
              </Form.Item>
              <Form.Item name="description" label={t('prompt.desc')} rules={[{ max: 255 }]} style={{ marginBottom: 0, flex: 1, minWidth: 260 }}>
                <Input placeholder={t('prompt.descPlaceholder')} maxLength={255} allowClear />
              </Form.Item>
            </div>
          </Form>
        </div>
      </Card>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: spacing.sm, marginBottom: spacing.sm }}>
        <MarkdownToolbar onAction={handleToolbar} />
        <Text type="secondary" style={{ fontSize: 12 }}>
          {content.length} / {CONTENT_MAX}
        </Text>
      </div>
      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: spacing.md, height: 'calc(100vh - 352px)', minHeight: 420 }}>
        <section style={paneStyle}>
          <div style={paneHeaderStyle}>
            <Text strong style={{ fontSize: 13 }}>
              {t('prompt.content')}
            </Text>
            <Text type="secondary" style={{ fontSize: 12 }}>
              Markdown
            </Text>
          </div>
          <Input.TextArea
            ref={textareaRef}
            value={content}
            onChange={(e) => setContent(e.target.value)}
            placeholder={t('prompt.editorPlaceholder')}
            variant="borderless"
            maxLength={CONTENT_MAX}
            style={{ flex: 1, minHeight: 0, resize: 'none', padding: spacing.md, fontSize: 13, fontFamily: 'SFMono-Regular, Consolas, "Liberation Mono", Menlo, monospace' }}
          />
        </section>
        <section style={paneStyle}>
          <div style={paneHeaderStyle}>
            <Text strong style={{ fontSize: 13 }}>
              {t('prompt.preview')}
            </Text>
            <Text type="secondary" style={{ fontSize: 12 }}>
              {t('prompt.previewLive')}
            </Text>
          </div>
          <div style={{ flex: 1, minHeight: 0, overflow: 'auto', padding: `8px ${spacing.md}px` }}>
            {content.trim() ? (
              <ReactMarkdownPreview content={content} />
            ) : (
              <Text type="secondary">{t('prompt.previewEmpty')}</Text>
            )}
          </div>
        </section>
      </div>
    </Page>
  )
}
