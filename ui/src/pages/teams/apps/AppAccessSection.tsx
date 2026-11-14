import { Button, Col, Form, Input, InputNumber, Row, Select, Space, Spin, Switch, Typography } from 'antd'
import { CopyOutlined, DownloadOutlined } from '@ant-design/icons'
import { useCallback, useEffect, useState, type CSSProperties } from 'react'
import { useTranslation } from 'react-i18next'
import { Card as DSCard, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { getAppAccessPoint, saveAppAccessPoint, type AppAccessPointConfig } from '@/api/accessPoint'
import { uploadImageWithKey } from '@/utils/storage'
import { Env } from '@/config/env'

/** 角色：0=Member 1=Admin 2=Owner（对齐后端 TeamRole 枚举） */
const ROLE_MEMBER = 0

interface AppAccessSectionProps {
  appId: string
  isExternal?: boolean
  canManage: boolean
  userRole?: number
}

interface AccessPointFormValues {
  title?: string | null
  subtitle?: string | null
  placeholder?: string | null
  primaryColor?: string | null
  position?: string
  displayMode?: string
  launcherText?: string | null
  avatar?: string | null
  panelWidth: number
  panelHeight: number
  defaultOpen: boolean
  enabled: boolean
}

export function AppAccessSection({ appId, isExternal, canManage, userRole }: AppAccessSectionProps) {
  const { t } = useTranslation()
  const [form] = Form.useForm<AccessPointFormValues>()
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [config, setConfig] = useState<AppAccessPointConfig | null>(null)
  const [uploadingAvatar, setUploadingAvatar] = useState(false)

  const isAdmin = (userRole ?? -1) > ROLE_MEMBER

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const res = await getAppAccessPoint(appId)
      setConfig(res)
      form.setFieldsValue({
        title: res.title ?? undefined,
        subtitle: res.subtitle ?? undefined,
        placeholder: res.placeholder ?? undefined,
        primaryColor: res.primaryColor ?? undefined,
        position: res.position ?? 'bottomRight',
        displayMode: res.displayMode ?? 'floating',
        launcherText: res.launcherText ?? undefined,
        avatar: res.avatar ?? undefined,
        panelWidth: res.panelWidth ?? 380,
        panelHeight: res.panelHeight ?? 560,
        defaultOpen: res.defaultOpen ?? false,
        enabled: res.enabled ?? true,
      })
    } finally {
      setLoading(false)
    }
  }, [appId, form])

  useEffect(() => {
    load()
  }, [load])

  const serverUrl = Env.serverUrl.replace(/\/+$/, '')
  // 嵌入代码用站点自身源（开发期即前端 dev server，生产为同源部署）；widget 默认以 script 源调用 API，
  // 开发期由 Vite 代理 /api 到后端，分离部署可在 script 上加 data-server 指向后端
  const siteOrigin = window.location.origin
  // 显示形式与面板高度随表单联动刷新嵌入片段
  const displayMode = Form.useWatch('displayMode', form) ?? 'floating'
  const panelHeightWatch = Form.useWatch('panelHeight', form)
  const isInline = displayMode === 'inline'
  const panelHeight = typeof panelHeightWatch === 'number' ? panelHeightWatch : 620
  const inlineTargetAttrs = isInline
    ? [`<div id="moai-chat" style="height:${typeof panelHeight === 'number' ? panelHeight : 620}px"></div>`]
    : []
  const inlineAttr = isInline ? `\n        data-mode="inline"\n        data-target="#moai-chat"` : ''
  const inlineMountAttr = isInline ? `,\n    mode: 'inline',\n    target: '#moai-chat'` : ''
  const preStyle: CSSProperties = {
    margin: 0,
    padding: spacing.sm,
    background: 'rgba(128,128,128,0.08)',
    borderRadius: 8,
    fontSize: 12,
    whiteSpace: 'pre-wrap',
    wordBreak: 'break-all',
  }

  // 外部应用：匿名开放的悬浮组件嵌入片段（is_auth 已退役，匿名换 token 即可）
  const basicSnippet = [
    `<!-- 可选：data-external-user-id 绑定外部身份以继承会话；data-nickname 外部用户显示名；data-server 后端地址（分离部署时） -->`,
    ...inlineTargetAttrs,
    `<script src="${siteOrigin}/embed/moai-widget.js"`,
    `        data-app-id="${appId}"${inlineAttr}`,
    `></script>`,
  ].join('\n')

  const chatEndpoint = `POST ${serverUrl}/api/teamres/agent/${appId}/chat`

  // 内部应用：身份穿透接入——业务后端颁发访问用户 token，前端按 AG-UI 协议直连（免组件）
  const aguiSnippet = [
    `# 1) 业务后端为用户颁发访问用户 token（系统级接入凭据调用，key 不落前端）`,
    `POST ${serverUrl}/api/subsys/system/app-tokens`,
    `{ "appId": "${appId}", "oauthProviderId": "<第三方登录渠道id>", "oauthSub": "<第三方用户id>" }`,
    `# → { accessToken, refreshToken, expiresIn, userId }`,
    `# 未绑定第三方身份时返回 404：请先经 POST /api/subsys/system/users 建用户并携带 oauth 绑定`,
    ``,
    `# 2) 前端持 token 直连（AG-UI 协议，SSE；token 存业务站点的浏览器存储，与其域名绑定）`,
    `POST ${serverUrl}/api/teamres/agent/${appId}/chat          # 对话（SSE）`,
    `POST/GET ${serverUrl}/api/teamres/agent/${appId}/session   # 创建/列表会话`,
    `GET ${serverUrl}/api/teamres/session/{sessionId}/messages  # 会话消息`,
    `POST ${serverUrl}/api/teamres/token/refresh                # 刷新轮换`,
    ``,
    `Authorization: Bearer <accessToken>`,
  ].join('\n')

  // 内部应用：悬浮组件 + tokenProvider（token 由业务后端签发注入，key 不落前端）
  const widgetTokenSnippet = [
    ...inlineTargetAttrs,
    `<script src="${serverUrl}/embed/moai-widget.js"></script><!-- 可下载后自托管到业务站点 -->`,
    `<script>`,
    `  MoaiWidget.mount({`,
    `    appId: '${appId}',`,
    `    server: '${serverUrl}'${inlineMountAttr},`,
    `    tokenProvider: function () {`,
    `      // 向你的业务后端索取已颁发的访问用户 token`,
    `      return fetch('/your-backend/moai-access-token').then(function (r) { return r.json() })`,
    `    }`,
    `  })`,
    `</script>`,
  ].join('\n')

  const copyText = async (text: string, message: string) => {
    await navigator.clipboard.writeText(text)
    feedback.success(message)
  }

  const handleSave = async (values: AccessPointFormValues) => {
    setSaving(true)
    try {
      await saveAppAccessPoint(appId, {
        title: values.title ?? null,
        subtitle: values.subtitle ?? null,
        placeholder: values.placeholder ?? null,
        primaryColor: values.primaryColor ?? null,
        position: values.position ?? 'bottomRight',
        displayMode: values.displayMode ?? 'floating',
        launcherText: values.launcherText ?? null,
        avatar: values.avatar ?? null,
        panelWidth: values.panelWidth,
        panelHeight: values.panelHeight,
        defaultOpen: values.defaultOpen,
        enabled: values.enabled,
      })
      feedback.success(t('accessPoint.saved'))
    } finally {
      setSaving(false)
    }
  }

  const handleUploadAvatar = async (file: File) => {
    setUploadingAvatar(true)
    try {
      const uploaded = await uploadImageWithKey(file)
      form.setFieldsValue({ avatar: uploaded.objectKey })
      feedback.success(t('accessPoint.avatarSuccess'))
    } catch {
      feedback.error(t('accessPoint.avatarFailed'))
    } finally {
      setUploadingAvatar(false)
    }
  }

  return (
    <Spin spinning={loading}>
      <Row gutter={[spacing.md, spacing.md]}>
        <Col xs={24} lg={15} xxl={16}>
          <DSCard title={t('accessPoint.configTitle')}>
            <Form form={form} layout="vertical" disabled={!canManage} onFinish={handleSave} initialValues={{ position: 'bottomRight', displayMode: 'floating', panelWidth: 380, panelHeight: 560, defaultOpen: false, enabled: true }}>
              <Row gutter={[spacing.md, spacing.md]}>
                <Col xs={24} md={12}>
                  <Form.Item name="title" label={t('accessPoint.fieldTitle')}>
                    <Input maxLength={100} placeholder={t('accessPoint.titlePlaceholder')} />
                  </Form.Item>
                </Col>
                <Col xs={24} md={12}>
                  <Form.Item name="launcherText" label={t('accessPoint.fieldLauncherText')}>
                    <Input maxLength={50} placeholder={t('accessPoint.launcherPlaceholder')} />
                  </Form.Item>
                </Col>
                <Col span={24}>
                  <Form.Item name="subtitle" label={t('accessPoint.fieldSubtitle')}>
                    <Input.TextArea rows={2} maxLength={255} />
                  </Form.Item>
                </Col>
                <Col xs={24} md={12}>
                  <Form.Item name="placeholder" label={t('accessPoint.fieldPlaceholder')}>
                    <Input maxLength={100} />
                  </Form.Item>
                </Col>
                <Col xs={24} md={12}>
                  <Form.Item name="primaryColor" label={t('accessPoint.fieldPrimaryColor')} rules={[{ pattern: /^#[0-9a-fA-F]{6}$/, message: t('accessPoint.colorInvalid') }]}>
                    <Input maxLength={7} placeholder="#1677ff" />
                  </Form.Item>
                </Col>
                <Col xs={24} md={12}>
                  <Form.Item name="displayMode" label={t('accessPoint.fieldDisplayMode')} rules={[{ required: true }]}>
                    <Select
                      options={[
                        { value: 'floating', label: t('accessPoint.displayModeFloating') },
                        { value: 'inline', label: t('accessPoint.displayModeInline') },
                      ]}
                    />
                  </Form.Item>
                </Col>
                {!isInline && (
                  <Col xs={24} md={12}>
                    <Form.Item name="position" label={t('accessPoint.fieldPosition')}>
                      <Select
                        options={[
                          { value: 'bottomRight', label: t('accessPoint.positionBottomRight') },
                          { value: 'bottomLeft', label: t('accessPoint.positionBottomLeft') },
                        ]}
                      />
                    </Form.Item>
                  </Col>
                )}
                <Col xs={24} md={12}>
                  <Form.Item name="avatar" label={t('accessPoint.fieldAvatar')}>
                    <Space>
                      <input
                        type="file"
                        accept="image/png,image/jpeg,image/webp"
                        disabled={!canManage || uploadingAvatar}
                        onChange={(e) => {
                          const file = e.target.files?.[0]
                          if (file) handleUploadAvatar(file)
                          e.target.value = ''
                        }}
                      />
                      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                        {form.getFieldValue('avatar') || t('accessPoint.avatarEmpty')}
                      </Typography.Text>
                    </Space>
                  </Form.Item>
                </Col>
                <Col xs={12} md={6}>
                  <Form.Item name="panelWidth" label={t('accessPoint.fieldPanelWidth')} rules={[{ required: true }]}>
                    <InputNumber min={280} max={640} style={{ width: '100%' }} />
                  </Form.Item>
                </Col>
                <Col xs={12} md={6}>
                  <Form.Item name="panelHeight" label={t('accessPoint.fieldPanelHeight')} rules={[{ required: true }]}>
                    <InputNumber min={360} max={900} style={{ width: '100%' }} />
                  </Form.Item>
                </Col>
                <Col xs={12} md={6}>
                  <Form.Item name="defaultOpen" label={t('accessPoint.fieldDefaultOpen')} valuePropName="checked">
                    <Switch />
                  </Form.Item>
                </Col>
                <Col xs={12} md={6}>
                  <Form.Item name="enabled" label={t('accessPoint.fieldEnabled')} valuePropName="checked">
                    <Switch />
                  </Form.Item>
                </Col>
              </Row>
              {canManage && (
                <Button type="primary" htmlType="submit" loading={saving}>
                  {t('accessPoint.save')}
                </Button>
              )}
            </Form>
          </DSCard>
        </Col>
        <Col xs={24} lg={9} xxl={8}>
          <Space direction="vertical" style={{ width: '100%' }} size={spacing.md}>
            {isExternal ? (
              <>
                <DSCard title={t('accessPoint.endpointTitle')}>
                  <Typography.Paragraph copyable={{ text: chatEndpoint }} style={{ marginBottom: 0, wordBreak: 'break-all' }}>
                    {chatEndpoint}
                  </Typography.Paragraph>
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    {t('accessPoint.endpointHint')}
                  </Typography.Text>
                </DSCard>
                <DSCard title={t('accessPoint.embedTitle')}>
                  {isAdmin ? (
                    <>
                      <pre style={preStyle}>{basicSnippet}</pre>
                      <Space style={{ marginTop: spacing.sm }}>
                        <Button icon={<CopyOutlined />} onClick={() => copyText(basicSnippet, t('accessPoint.copied'))}>
                          {t('accessPoint.copySnippet')}
                        </Button>
                        <Button icon={<DownloadOutlined />} href={`${serverUrl}/embed/moai-widget.js`} download="moai-widget.js">
                          {t('accessPoint.downloadJs')}
                        </Button>
                      </Space>
                    </>
                  ) : (
                    <Typography.Text type="secondary">{t('accessPoint.adminOnly')}</Typography.Text>
                  )}
                </DSCard>
              </>
            ) : (
              <>
                <DSCard title={t('accessPoint.internalHintTitle')}>
                  <Typography.Paragraph style={{ marginBottom: spacing.sm }}>
                    {t('accessPoint.internalHint')}
                  </Typography.Paragraph>
                  {isAdmin && (
                    <>
                      <Typography.Text strong style={{ fontSize: 12 }}>{t('accessPoint.tokenEndpointLabel')}</Typography.Text>
                      <Typography.Paragraph copyable={{ text: `${serverUrl}/api/subsys/system/app-tokens` }} style={{ marginBottom: 0, wordBreak: 'break-all' }}>
                        {`POST ${serverUrl}/api/subsys/system/app-tokens`}
                      </Typography.Paragraph>
                      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                        {t('accessPoint.tokenEndpointHint')}
                      </Typography.Text>
                    </>
                  )}
                </DSCard>
                {isAdmin && config && (
                  <>
                    <DSCard title={t('accessPoint.aguiTitle')}>
                      <pre style={preStyle}>{aguiSnippet}</pre>
                      <Space style={{ marginTop: spacing.sm }}>
                        <Button icon={<CopyOutlined />} onClick={() => copyText(aguiSnippet, t('accessPoint.copied'))}>
                          {t('accessPoint.copySnippet')}
                        </Button>
                      </Space>
                    </DSCard>
                    <DSCard title={t('accessPoint.widgetTokenTitle')}>
                      <pre style={preStyle}>{widgetTokenSnippet}</pre>
                      <Space style={{ marginTop: spacing.sm }}>
                        <Button icon={<CopyOutlined />} onClick={() => copyText(widgetTokenSnippet, t('accessPoint.copied'))}>
                          {t('accessPoint.copySnippet')}
                        </Button>
                        <Button icon={<DownloadOutlined />} href={`${serverUrl}/embed/moai-widget.js`} download="moai-widget.js">
                          {t('accessPoint.downloadJs')}
                        </Button>
                      </Space>
                    </DSCard>
                  </>
                )}
                {!isAdmin && (
                  <DSCard title={t('accessPoint.aguiTitle')}>
                    <Typography.Text type="secondary">{t('accessPoint.adminOnly')}</Typography.Text>
                  </DSCard>
                )}
              </>
            )}
          </Space>
        </Col>
      </Row>
    </Spin>
  )
}
