import { useCallback, useEffect, useMemo, useState } from 'react'
import { UploadOutlined } from '@ant-design/icons'
import { Avatar, Button, Card, Form, Input, Select, Space, Upload, Typography } from 'antd'
import type { UploadProps } from 'antd'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams } from 'react-router'
import { Page, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { classifyApi, ClassifyType, type Classify } from '@/api/classify'
import { createPrompt, getPromptDetail, setPromptAvatar, updatePrompt } from '@/api/prompt'
import { resolveStorageUrl, uploadImageWithKey } from '@/utils/storage'
import { ReactMarkdownPreview } from './PromptDetailModal'

const { Text } = Typography

interface PromptEditorFormValues {
  name?: string
  promptClassId?: number
  description?: string
}

/**
 * 提示词编辑器独立页：左侧 Markdown 编辑、右侧实时预览，支持上传头像。
 * 路由：/prompts/new、/prompts/:promptId/edit（个人）；/team/:teamId/prompt/new、/team/:teamId/prompt/:promptId/edit（团队）。
 */
export function PromptEditor() {
  const { t } = useTranslation()
  const navigate = useNavigate()
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
      <Card styles={{ body: { padding: spacing.lg } }} loading={loading}>
        <Space size={spacing.lg} align="start" wrap style={{ marginBottom: spacing.lg }}>
          <div>
            <Upload beforeUpload={avatarBeforeUpload} showUploadList={false} accept="image/*">
              <Button type="text" loading={avatarUploading} style={{ padding: 0 }}>
                <Avatar size={64} src={avatarObjectKey ? resolveStorageUrl(avatarObjectKey) : undefined}>
                  {(nameValue ?? '?').slice(0, 1).toUpperCase()}
                </Avatar>
              </Button>
            </Upload>
            <div style={{ textAlign: 'center', marginTop: 4 }}>
              <Text type="secondary" style={{ fontSize: 12 }}>
                <UploadOutlined /> {t('prompt.avatar')}
              </Text>
            </div>
          </div>
          <Form form={form} layout="vertical" style={{ flex: 1, minWidth: 320 }}>
            <Space size={spacing.md} wrap align="start">
              <Form.Item
                name="name"
                label={t('prompt.name')}
                rules={[{ required: true, message: t('prompt.namePlaceholder') }, { max: 20, message: `${t('prompt.name')} ≤ 20` }]}
                style={{ marginBottom: 0, minWidth: 220 }}
              >
                <Input placeholder={t('prompt.namePlaceholder')} maxLength={20} />
              </Form.Item>
              <Form.Item name="promptClassId" label={t('prompt.class')} style={{ marginBottom: 0, minWidth: 160 }}>
                <Select allowClear placeholder={t('prompt.classAll')} options={classOptions} style={{ width: 160 }} />
              </Form.Item>
              <Form.Item name="description" label={t('prompt.desc')} rules={[{ max: 255 }]} style={{ marginBottom: 0, flex: 1, minWidth: 260 }}>
                <Input placeholder={t('prompt.descPlaceholder')} maxLength={255} />
              </Form.Item>
            </Space>
          </Form>
        </Space>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: spacing.md }}>
          <div>
            <Text strong style={{ display: 'block', marginBottom: 8 }}>
              {t('prompt.content')}
            </Text>
            <Input.TextArea
              value={content}
              onChange={(e) => setContent(e.target.value)}
              placeholder={t('prompt.editorPlaceholder')}
              variant="filled"
              rows={20}
              maxLength={10000}
              showCount
              style={{ fontFamily: 'monospace' }}
            />
          </div>
          <div>
            <Text strong style={{ display: 'block', marginBottom: 8 }}>
              {t('prompt.preview')}
            </Text>
            <div
              style={{
                border: '1px solid rgba(128,128,128,0.25)',
                borderRadius: 8,
                padding: '8px 16px',
                minHeight: 480,
                maxHeight: 560,
                overflow: 'auto',
                background: 'rgba(128,128,128,0.04)',
              }}
            >
              {content.trim() ? (
                <ReactMarkdownPreview content={content} />
              ) : (
                <Text type="secondary">{t('prompt.previewEmpty')}</Text>
              )}
            </div>
          </div>
        </div>
      </Card>
    </Page>
  )
}
