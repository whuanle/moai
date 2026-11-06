import { useCallback, useEffect, useMemo, useState, type CSSProperties, type ReactNode } from 'react'
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { Avatar, Button, Col, Form, Input, Row, Select, Space, Spin, Tooltip, Typography, theme } from 'antd'
import { Link, useNavigate, useParams } from 'react-router'
import { useTranslation } from 'react-i18next'
import { AvatarUpload, Page, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { VditorMarkdownEditor } from '@/components/VditorMarkdownEditor'
import { resolveStorageUrl, uploadImageWithKey } from '@/utils/storage'
import { getTeamPlugins, type TeamPluginItemType } from '@/api/team-plugin'
import { getWikis, type WikiItem } from '@/api/wiki'
import { getKnowledgeGraphs, type KnowledgeGraphItem } from '@/api/knowledgeGraph'
import {
  createTeamAgent,
  getTeamAgent,
  setTeamAgentAvatar,
  updateTeamAgent,
} from '@/api/team-agent'

const { Text } = Typography

/** 编辑页主提示词上限（与后端校验一致） */
const MAIN_PROMPT_MAX = 50000
/** 子智能体提示词上限（与后端校验一致） */
const SUB_PROMPT_MAX = 30000
/** 主智能体提示词编辑器高度（左列信息区与之等高匹配） */
const MAIN_PROMPT_HEIGHT = 620
/** 子智能体提示词编辑器高度 */
const SUB_PROMPT_HEIGHT = 420

/** Form.Item 注入 value/onChange 需要可缺省 props，包一层对齐 VditorMarkdownEditor 必填签名 */
function SubPromptEditor(props: { value?: string; onChange?: (v: string) => void; placeholder?: string; maxLength?: number; style?: CSSProperties }) {
  return (
    <VditorMarkdownEditor
      value={props.value ?? ''}
      onChange={props.onChange ?? (() => undefined)}
      mode="ir"
      placeholder={props.placeholder}
      maxLength={props.maxLength}
      style={props.style}
    />
  )
}

/** 主智能体 + N 个子智能体的编辑页：无卡片容器，左右 3:7（左信息 / 右提示词编辑器） */
export function TeamAgentEditor() {
  const { t } = useTranslation()
  const params = useParams<{ teamId: string; agentId?: string }>()
  const teamId = Number(params.teamId)
  const agentId = params.agentId
  const isEdit = !!agentId
  const navigate = useNavigate()
  const { token } = theme.useToken()

  const [form] = Form.useForm()
  const [loading, setLoading] = useState(isEdit)
  const [saving, setSaving] = useState(false)
  const [mainPrompt, setMainPrompt] = useState('')

  // 依赖选项（插件/知识库/知识图谱，均团队范围）
  const [plugins, setPlugins] = useState<TeamPluginItemType[]>([])
  const [wikis, setWikis] = useState<WikiItem[]>([])
  const [graphs, setGraphs] = useState<KnowledgeGraphItem[]>([])

  // 头像：编辑时上传立即生效；创建时先暂存 objectKey 随创建一起提交
  const [avatarObjectKey, setAvatarObjectKey] = useState('')
  const [initialAvatarKey, setInitialAvatarKey] = useState('')
  const [avatarUploading, setAvatarUploading] = useState(false)
  // 启停不走编辑页（列表卡片直切），保存时保留原值（更新为整文档替换）
  const [enabledValue, setEnabledValue] = useState(true)

  const loadOptions = useCallback(async () => {
    if (!Number.isFinite(teamId) || teamId <= 0) return
    try {
      const [pluginRes, wikiRes, graphRes] = await Promise.all([
        getTeamPlugins(teamId),
        getWikis(teamId),
        getKnowledgeGraphs(teamId),
      ])
      setPlugins((pluginRes.items ?? []).filter((x) => x.pluginId))
      setWikis((wikiRes.items ?? []).filter((x) => x.wikiId != null))
      // 仅托管图（接入图不参与应用侧检索绑定）
      setGraphs((graphRes.items ?? []).filter((x) => x.mode === 'managed' && x.kgId != null))
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }, [teamId])

  const loadDetail = useCallback(async () => {
    if (!agentId) return
    setLoading(true)
    try {
      const detail = await getTeamAgent(agentId)
      form.setFieldsValue({
        key: detail.key ?? '',
        name: detail.name ?? '',
        description: detail.description ?? undefined,
        mainPluginIds: (detail.mainPluginIds ?? []).map(String),
        mainWikiIds: (detail.mainWikiIds ?? []).map(String),
        mainGraphIds: (detail.mainGraphIds ?? []).map(String),
        subs: (detail.subs ?? []).map((sub) => ({
          key: sub.key ?? '',
          name: sub.name ?? '',
          description: sub.description ?? '',
          prompt: sub.prompt ?? '',
          pluginIds: (sub.pluginIds ?? []).map(String),
          wikiIds: (sub.wikiIds ?? []).map(String),
          graphIds: (sub.graphIds ?? []).map(String),
        })),
      })
      setEnabledValue(detail.enabled ?? true)
      setMainPrompt(detail.mainPrompt ?? '')
      const avatar = detail.avatarObjectKey ?? ''
      setAvatarObjectKey(avatar)
      setInitialAvatarKey(avatar)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [agentId, form])

  useEffect(() => {
    void loadOptions()
  }, [loadOptions])

  useEffect(() => {
    void loadDetail()
  }, [loadDetail])

  /** 依赖下拉统一渲染：头像（存储路径解析，无头像回退首字母）+ 名称；label 节点同时作为选中 Tag 内容（Tag 里也带头像） */
  const renderDepOption = (item: { avatarObjectKey?: string | null; name: string; value: string }): { value: string; label: ReactNode; title: string } => {
    return {
      value: item.value,
      title: item.name,
      label: (
        <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6 }}>
          <Avatar shape="square" size={18} src={resolveStorageUrl(item.avatarObjectKey ?? null) || undefined} alt={item.name}>
            {item.name.slice(0, 1).toUpperCase()}
          </Avatar>
          <span>{item.name}</span>
        </span>
      ),
    }
  }

  const pluginSelectOptions = useMemo(
    () =>
      [
        {
          label: t('appManage.pluginGroupTeam'),
          title: t('appManage.pluginGroupTeam'),
          options: plugins
            .filter((item) => item.isTeamOwned)
            .map((item) => renderDepOption({ avatarObjectKey: item.avatarPath, name: item.title || item.pluginName || String(item.pluginId), value: String(item.pluginId) })),
        },
        {
          label: t('appManage.pluginGroupSystem'),
          title: t('appManage.pluginGroupSystem'),
          options: plugins
            .filter((item) => !item.isTeamOwned)
            .map((item) => renderDepOption({ avatarObjectKey: item.avatarPath, name: item.title || item.pluginName || String(item.pluginId), value: String(item.pluginId) })),
        },
      ].filter((group) => group.options.length > 0),
    [plugins, t],
  )

  const wikiSelectOptions = useMemo(
    () => wikis.map((item) => renderDepOption({ avatarObjectKey: item.avatarPath, name: item.name || String(item.wikiId), value: String(item.wikiId) })),
    [wikis],
  )

  const graphSelectOptions = useMemo(
    () => graphs.map((item) => renderDepOption({ avatarObjectKey: item.avatarPath, name: item.name || String(item.kgId), value: String(item.kgId) })),
    [graphs],
  )

  /** 头像上传：压缩后的图片走存储直传管线；编辑时立即落库，创建时暂存 objectKey */
  const handleAvatarSelect = async (file: File) => {
    if (!file.type.startsWith('image/')) {
      feedback.error(t('team.avatarTypeError'))
      return
    }
    if (file.size > 5 * 1024 * 1024) {
      feedback.error(t('team.avatarSizeError'))
      return
    }
    setAvatarUploading(true)
    try {
      const { objectKey } = await uploadImageWithKey(file)
      if (isEdit && agentId) {
        await setTeamAgentAvatar(agentId, objectKey)
        setInitialAvatarKey(objectKey)
      }
      setAvatarObjectKey(objectKey)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setAvatarUploading(false)
    }
  }

  const handleSave = async () => {
    const values = await form.validateFields()
    if (!mainPrompt.trim()) {
      feedback.error(t('teamAgents.mainPromptRequired'))
      return
    }
    const subs = (values.subs ?? []).map((sub: Record<string, unknown>) => ({
      key: String(sub.key ?? ''),
      name: String(sub.name ?? ''),
      description: String(sub.description ?? ''),
      prompt: String(sub.prompt ?? ''),
      pluginIds: (sub.pluginIds as string[] | undefined) ?? [],
      wikiIds: (sub.wikiIds as string[] | undefined) ?? [],
      graphIds: (sub.graphIds as string[] | undefined) ?? [],
    }))
    const payload = {
      key: String(values.key ?? ''),
      name: values.name,
      description: values.description ?? '',
      enabled: enabledValue,
      mainPrompt,
      mainPluginIds: (values.mainPluginIds as string[] | undefined) ?? [],
      mainWikiIds: (values.mainWikiIds as string[] | undefined) ?? [],
      mainGraphIds: (values.mainGraphIds as string[] | undefined) ?? [],
      subs,
    }
    setSaving(true)
    try {
      if (isEdit && agentId) {
        await updateTeamAgent(agentId, payload)
        if (avatarObjectKey !== initialAvatarKey && avatarObjectKey) {
          await setTeamAgentAvatar(agentId, avatarObjectKey)
        }
        feedback.success(t('teamAgents.saveSuccess'))
      } else {
        await createTeamAgent(teamId, payload, avatarObjectKey || undefined)
        feedback.success(t('teamAgents.createSuccess'))
      }
      navigate(`/team/${teamId}/agents`)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSaving(false)
    }
  }

  /** 分区标题行：左侧标题+提示，右侧动作（返回/保存），不单独占一行 */
  const sectionHeader = (title: string, hint: string | undefined, actions?: ReactNode) => (
    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: spacing.md, marginBottom: spacing.md }}>
      <Space size={spacing.sm} align="baseline">
        <Text strong style={{ fontSize: 15 }}>{title}</Text>
        {hint && <Text type="secondary" style={{ fontSize: 12 }}>{hint}</Text>}
      </Space>
      {actions && <Space size={spacing.sm}>{actions}</Space>}
    </div>
  )

  const promptLabel = (text: string, hint?: string) => (
    <Space size={spacing.sm} align="baseline" style={{ marginBottom: spacing.xs }}>
      <Text strong>{text}</Text>
      {hint && <Text type="secondary" style={{ fontSize: 12 }}>{hint}</Text>}
    </Space>
  )

  return (
    <Page
      breadcrumb={[{ title: <Link to={`/team/${teamId}/agents`}>{t('teamAgents.back')}</Link> }]}
    >
      <Form form={form} layout="vertical" component={false}>
        <Spin spinning={loading}>
        {/* ===== 主智能体：左 3 信息 / 右 7 主提示词编辑器 ===== */}
        <section>
          {sectionHeader(
            t('teamAgents.mainSectionTitle'),
            undefined,
            <Button type="primary" loading={saving} onClick={() => void handleSave()}>
              {t('teamAgents.save')}
            </Button>,
          )}
          <Row gutter={spacing.md}>
            <Col xs={24} lg={7}>
              <Space size={spacing.md} align="start" style={{ display: 'flex' }}>
                <AvatarUpload
                  src={avatarObjectKey ? resolveStorageUrl(avatarObjectKey) : undefined}
                  fallback={(form.getFieldValue('name') || '?').slice(0, 1)}
                  size={64}
                  uploading={avatarUploading}
                  onSelect={(file) => {
                    void handleAvatarSelect(file)
                  }}
                />
                <div style={{ flex: 1, minWidth: 0 }}>
                  <Tooltip title={isEdit ? t('teamAgents.keyImmutable') : t('teamAgents.keyRule')} placement="topLeft">
                    <Form.Item
                      name="key"
                      rules={[
                        { required: true, message: t('teamAgents.keyRequired') },
                        { pattern: /^[a-z][a-z0-9_]{0,29}$/, message: t('teamAgents.keyInvalid') },
                      ]}
                      style={{ marginBottom: spacing.sm }}
                    >
                      <Input placeholder={t('teamAgents.keyPlaceholder')} maxLength={30} addonBefore="key" disabled={isEdit} />
                    </Form.Item>
                  </Tooltip>
                  <Form.Item
                    name="name"
                    rules={[
                      { required: true, message: t('teamAgents.nameRequired') },
                      { max: 50, message: t('teamAgents.nameMax') },
                    ]}
                    style={{ marginBottom: 0 }}
                  >
                    <Input placeholder={t('teamAgents.namePlaceholder')} maxLength={50} />
                  </Form.Item>
                </div>
              </Space>
              <Form.Item name="description" rules={[{ max: 500, message: t('teamAgents.descMax') }]} style={{ marginTop: spacing.md }}>
                <Input.TextArea placeholder={t('teamAgents.descPlaceholder')} rows={3} maxLength={500} />
              </Form.Item>
              <Form.Item name="mainPluginIds" label={t('teamAgents.plugins')}>
                <Select mode="multiple" options={pluginSelectOptions} allowClear showSearch optionFilterProp="title" maxTagCount="responsive" placeholder={t('teamAgents.pluginsPlaceholder')} />
              </Form.Item>
              <Form.Item name="mainWikiIds" label={t('teamAgents.wikis')}>
                <Select mode="multiple" options={wikiSelectOptions} allowClear showSearch optionFilterProp="title" maxTagCount="responsive" placeholder={t('teamAgents.wikisPlaceholder')} />
              </Form.Item>
              <Form.Item name="mainGraphIds" label={t('teamAgents.graphs')}>
                <Select mode="multiple" options={graphSelectOptions} allowClear showSearch optionFilterProp="title" maxTagCount="responsive" placeholder={t('teamAgents.graphsPlaceholder')} />
              </Form.Item>
            </Col>
            <Col xs={24} lg={17}>
              {promptLabel(t('teamAgents.mainPromptTitle'), t('teamAgents.mainPromptHint'))}
              <VditorMarkdownEditor
                value={mainPrompt}
                onChange={setMainPrompt}
                mode="ir"
                placeholder={t('teamAgents.mainPromptPlaceholder')}
                maxLength={MAIN_PROMPT_MAX}
                style={{ height: MAIN_PROMPT_HEIGHT }}
              />
            </Col>
          </Row>
        </section>

        <div style={{ height: 1, background: token.colorSplit, margin: `${spacing.xl}px 0 ${spacing.lg}px` }} />

        {/* ===== 子智能体：每项左 3 信息 / 右 7 提示词编辑器（与主智能体同款编辑器） ===== */}
        <section>
          {sectionHeader(t('teamAgents.subSectionTitle'), t('teamAgents.subSectionHint'))}
          <Form.List name="subs">
            {(fields, { add, remove }) => (
              <>
                {fields.map((field) => (
                  <div
                    key={field.key}
                    style={{ paddingBottom: spacing.md, marginBottom: spacing.md, borderBottom: `1px solid ${token.colorSplit}` }}
                  >
                    <Row gutter={spacing.md}>
                      <Col xs={24} lg={7}>
                        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: spacing.sm }}>
                          <Text type="secondary" style={{ fontSize: 12 }}>{`${t('teamAgents.subSectionTitle')} ${field.name + 1}`}</Text>
                          <Button type="text" size="small" danger icon={<DeleteOutlined />} onClick={() => remove(field.name)} />
                        </div>
                        <Tooltip title={t('teamAgents.subKeyRule')} placement="topLeft">
                          <Form.Item
                            name={[field.name, 'key']}
                            rules={[
                              { required: true, message: t('teamAgents.subKeyRequired') },
                              { pattern: /^[a-z][a-z0-9_]{0,29}$/, message: t('teamAgents.subKeyInvalid') },
                            ]}
                          >
                            <Input placeholder={t('teamAgents.subKeyPlaceholder')} maxLength={30} addonBefore="key" />
                          </Form.Item>
                        </Tooltip>
                        <Form.Item
                          name={[field.name, 'name']}
                          rules={[
                            { required: true, message: t('teamAgents.subNameRequired') },
                            { max: 50, message: t('teamAgents.subNameMax') },
                          ]}
                        >
                          <Input placeholder={t('teamAgents.subNamePlaceholder')} maxLength={50} />
                        </Form.Item>
                        <Form.Item name={[field.name, 'pluginIds']}>
                          <Select mode="multiple" options={pluginSelectOptions} allowClear showSearch optionFilterProp="title" maxTagCount="responsive" placeholder={t('teamAgents.pluginsPlaceholder')} />
                        </Form.Item>
                        <Form.Item name={[field.name, 'wikiIds']}>
                          <Select mode="multiple" options={wikiSelectOptions} allowClear showSearch optionFilterProp="title" maxTagCount="responsive" placeholder={t('teamAgents.wikisPlaceholder')} />
                        </Form.Item>
                        <Form.Item name={[field.name, 'graphIds']}>
                          <Select mode="multiple" options={graphSelectOptions} allowClear showSearch optionFilterProp="title" maxTagCount="responsive" placeholder={t('teamAgents.graphsPlaceholder')} />
                        </Form.Item>
                        <Form.Item
                          name={[field.name, 'description']}
                          rules={[
                            { required: true, message: t('teamAgents.subDescRequired') },
                            { max: 500, message: t('teamAgents.subDescMax') },
                          ]}
                          extra={t('teamAgents.subDescHint')}
                        >
                          <Input.TextArea placeholder={t('teamAgents.subDescPlaceholder')} rows={4} maxLength={500} />
                        </Form.Item>
                      </Col>
                      <Col xs={24} lg={17}>
                        {promptLabel(t('teamAgents.subPromptTitle'))}
                        <Form.Item
                          name={[field.name, 'prompt']}
                          rules={[
                            { required: true, message: t('teamAgents.subPromptRequired') },
                            { max: SUB_PROMPT_MAX, message: `${t('teamAgents.subPromptMax')} ${SUB_PROMPT_MAX}` },
                          ]}
                          style={{ marginBottom: 0 }}
                        >
                          <SubPromptEditor
                            placeholder={t('teamAgents.subPromptPlaceholder')}
                            maxLength={SUB_PROMPT_MAX}
                            style={{ height: SUB_PROMPT_HEIGHT }}
                          />
                        </Form.Item>
                      </Col>
                    </Row>
                  </div>
                ))}
                <Button
                  type="dashed"
                  block
                  icon={<PlusOutlined />}
                  onClick={() => add({ key: '', name: '', description: '', prompt: '', pluginIds: [], wikiIds: [], graphIds: [] })}
                  disabled={fields.length >= 20}
                >
                  {t('teamAgents.addSub')}
                </Button>
              </>
            )}
          </Form.List>
        </section>
        </Spin>
      </Form>
    </Page>
  )
}
