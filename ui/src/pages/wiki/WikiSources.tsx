import { useCallback, useEffect, useMemo, useState } from 'react'
import {
  ClockCircleOutlined,
  DeleteOutlined,
  EditOutlined,
  FileTextOutlined,
  GlobalOutlined,
  PlusOutlined,
  ReloadOutlined,
  SyncOutlined,
} from '@ant-design/icons'
import { Button, Drawer, Form, Input, Modal, Popconfirm, Space, Switch, Tag, Tooltip, Typography } from 'antd'
import type { TableColumnsType } from 'antd'
import { useTranslation } from 'react-i18next'
import { DataTable, feedback } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { formatDateTime } from '@/utils/datetime'
import {
  createWikiSource,
  deleteWikiSource,
  getWikiSources,
  getWikiSourceDocuments,
  syncWikiSource,
  updateWikiSource,
  WIKI_SOURCE_DOC_FAILED,
  WIKI_SOURCE_DOC_PENDING,
  WIKI_SOURCE_DOC_SYNCED,
  WIKI_SOURCE_SYNC_FAILED,
  WIKI_SOURCE_SYNC_NONE,
  WIKI_SOURCE_SYNC_SUCCESS,
  WIKI_SOURCE_SYNC_SYNCING,
  WIKI_SOURCE_TYPE_CRAWLER,
  WIKI_SOURCE_TYPE_FEISHU,
  type WikiSourceDocumentItem,
  type WikiSourceItem,
  type WikiSourceSyncStatus,
  type WikiSourceType,
} from '@/api/wiki'
import { WikiSourceCrawlerFormFields } from './WikiSourceCrawlerForm'
import { DEFAULT_CRAWLER_VALUES, type WikiSourceCrawlerFormValues } from './wikiSourceCrawler'
import { WikiSourceWorkflowFields } from './WikiSourceWorkflowFields'

const { Text, Link: AntLink } = Typography

/** 角色：0=Member 1=Admin 2=Owner（对齐后端 TeamRole 枚举） */
const ROLE_MEMBER = 0

/** cron 快选预设：统一按 UTC 提交，界面上明确标注 */
const CRON_PRESETS = [
  { key: 'hourly', value: '0 * * * *' },
  { key: 'every6h', value: '0 */6 * * *' },
  { key: 'daily', value: '0 2 * * *' },
  { key: 'weekly', value: '0 2 * * 1' },
] as const

/** 外部源编辑弹窗表单值：爬虫字段 + 飞书字段 + 通用字段合并声明，按外部源类型只渲染/提交其一 */
interface SourceFormValues extends WikiSourceCrawlerFormValues {
  name: string
  description?: string
  cron: string
  isEnable: boolean
  nodeToken?: string
  feishuAppId?: string
  newAppName?: string
  newAppId?: string
  newAppSecret?: string
  newAppDomain?: string
  includeSubNodes?: boolean
  /** 飞书源遍历深度（与爬虫 maxDepth 同名冲突，故单列字段名） */
  feishuMaxDepth?: number
  maxDocuments?: number
  isEventSubscription?: boolean
}

interface WikiSourcesProps {
  wikiId: number
  /**
   * 当前用户在知识库所属团队的角色，用于前端门禁展示（后端仍会二次校验）。
   * 取值 0=Member 1=Admin 2=Owner。显式传入时直接作为初始权限，避免接口返回前的空档期把按钮渲染成可点。
   */
  myRole?: number | null
}

/**
 * 知识库外部源管理：列表 + 新建（「新建飞书源 / 新建爬虫」双入口弹窗）+ 编辑弹窗 + 立即同步 + 定时爬取 + 已同步文档查看。
 * 外部源类型由入口按钮决定（编辑时取记录类型，创建后不可变更）；网页爬虫源支持抓取频率（请求间隔）与深度/页数上限，
 * 避免把目标站点抓崩；定时爬取由后端 Hangfire 后台执行。
 */
export function WikiSources({ wikiId, myRole }: WikiSourcesProps) {
  const { t } = useTranslation()

  const [loading, setLoading] = useState(false)
  const [items, setItems] = useState<WikiSourceItem[]>([])
  const [role, setRole] = useState<number | null>(myRole ?? null)
  const isAdminPlus = role != null && role !== ROLE_MEMBER

  // 新建 / 编辑弹窗
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<WikiSourceItem | null>(null)
  /** 当前弹窗对应的外部源类型：新建由入口按钮决定，编辑取记录类型（类型创建后不可变更） */
  const [activeType, setActiveType] = useState<WikiSourceType>(WIKI_SOURCE_TYPE_FEISHU)
  const [submitting, setSubmitting] = useState(false)
  const [form] = Form.useForm<SourceFormValues>()
  const [cronEnabled, setCronEnabled] = useState(false)

  // 同步中/同步结果
  const [syncingId, setSyncingId] = useState<string | null>(null)

  // 文档抽屉
  const [docDrawerOpen, setDocDrawerOpen] = useState(false)
  const [docSource, setDocSource] = useState<WikiSourceItem | null>(null)
  const [docLoading, setDocLoading] = useState(false)
  const [docs, setDocs] = useState<WikiSourceDocumentItem[]>([])
  const [docTotal, setDocTotal] = useState(0)
  const [docPageNo, setDocPageNo] = useState(1)
  const [docPageSize, setDocPageSize] = useState(20)

  const load = useCallback(async () => {
    if (!Number.isFinite(wikiId) || wikiId <= 0) return
    setLoading(true)
    try {
      const res = await getWikiSources(wikiId)
      setItems(res.items ?? [])
      // 接口的 myRole 优先；接口未返回时保留 props 传入的初始角色
      if (res.myRole != null) setRole(res.myRole)
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [wikiId])

  useEffect(() => {
    void load()
  }, [load])

  const openCreate = (sourceType: WikiSourceType) => {
    setEditing(null)
    setActiveType(sourceType)
    setCronEnabled(false)
    form.setFieldsValue({
      ...DEFAULT_CRAWLER_VALUES,
      name: '',
      description: undefined,
      cron: CRON_PRESETS[2].value,
      isEnable: true,
    })
    setModalOpen(true)
  }

  const openEdit = (record: WikiSourceItem) => {
    setEditing(record)
    const isCrawler = record.sourceType === WIKI_SOURCE_TYPE_CRAWLER
    setActiveType(isCrawler ? WIKI_SOURCE_TYPE_CRAWLER : WIKI_SOURCE_TYPE_FEISHU)
    const crawler = record.crawler
    const cron = (record.cron ?? '').trim()
    setCronEnabled(cron.length > 0)
    form.setFieldsValue({
      ...(isCrawler
        ? {
            startUrl: crawler?.startUrl ?? '',
            pathPrefix: crawler?.pathPrefix ?? '',
            maxDepth: crawler?.maxDepth && crawler.maxDepth > 0 ? crawler.maxDepth : DEFAULT_CRAWLER_VALUES.maxDepth,
            maxPages: crawler?.maxPages && crawler.maxPages > 0 ? crawler.maxPages : DEFAULT_CRAWLER_VALUES.maxPages,
            requestIntervalSeconds:
              crawler?.requestIntervalSeconds && crawler.requestIntervalSeconds > 0
                ? crawler.requestIntervalSeconds
                : DEFAULT_CRAWLER_VALUES.requestIntervalSeconds,
            timeoutSeconds:
              crawler?.timeoutSeconds && crawler.timeoutSeconds > 0 ? crawler.timeoutSeconds : DEFAULT_CRAWLER_VALUES.timeoutSeconds,
            userAgent: crawler?.userAgent ?? '',
            contentSelector: crawler?.contentSelector ?? '',
            isOverwriteExisting: crawler?.isOverwriteExisting ?? true,
          }
        : {}),
      name: record.name ?? '',
      description: record.description ?? undefined,
      cron: cron || CRON_PRESETS[2].value,
      isEnable: record.isEnable ?? true,
    })
    setModalOpen(true)
  }

  const handleSubmit = async () => {
    const values = await form.validateFields()
    const isCrawler = activeType === WIKI_SOURCE_TYPE_CRAWLER
    const payload = {
      name: values.name.trim(),
      description: values.description?.trim() || undefined,
      crawler: isCrawler
        ? {
            startUrl: values.startUrl.trim(),
            pathPrefix: values.pathPrefix?.trim() ?? '',
            maxDepth: values.maxDepth,
            maxPages: values.maxPages,
            requestIntervalSeconds: values.requestIntervalSeconds,
            timeoutSeconds: values.timeoutSeconds,
            userAgent: values.userAgent?.trim() || null,
            contentSelector: values.contentSelector?.trim() || null,
            isOverwriteExisting: values.isOverwriteExisting,
          }
        : undefined,
      // 关闭定时同步时提交空串（后端以空串表示关闭）
      cron: cronEnabled ? values.cron.trim() : '',
      isEnable: values.isEnable,
    }
    // 飞书源更新时不下发 crawler，避免后端按爬虫口径校验一份并未使用的配置
    const updatePayload = isCrawler ? payload : { ...payload, crawler: undefined }
    setSubmitting(true)
    try {
      if (editing?.sourceId) {
        // 后端 Update 命令没有 sourceType 字段，也不接受飞书专属字段：只提交通用字段 + 爬虫配置，
        // 否则会在「爬虫源」上触发飞书绑定互斥校验（FeishuAppId / NewAppId 二选一）
        await updateWikiSource(wikiId, editing.sourceId, updatePayload)
        feedback.success(t('wiki.source.updateSuccess'))
      } else {
        await createWikiSource(wikiId, {
          ...payload,
          sourceType: activeType,
          // 飞书文档源沿用原有绑定参数（选择已有连接或新建连接）
          feishuAppId: isCrawler ? undefined : (form.getFieldValue('feishuAppId') as string | undefined) ?? null,
          newAppName: isCrawler ? undefined : (form.getFieldValue('newAppName') as string | undefined) ?? null,
          newAppId: isCrawler ? undefined : (form.getFieldValue('newAppId') as string | undefined) ?? null,
          newAppSecret: isCrawler ? undefined : (form.getFieldValue('newAppSecret') as string | undefined) ?? null,
          newAppDomain: isCrawler ? undefined : (form.getFieldValue('newAppDomain') as string | undefined) ?? null,
          nodeToken: isCrawler ? undefined : (form.getFieldValue('nodeToken') as string | undefined) ?? null,
          includeSubNodes: isCrawler ? undefined : (form.getFieldValue('includeSubNodes') as boolean | undefined),
          maxDepth: isCrawler ? undefined : (form.getFieldValue('feishuMaxDepth') as number | undefined),
          maxDocuments: isCrawler ? undefined : (form.getFieldValue('maxDocuments') as number | undefined),
          isEventSubscription: isCrawler ? undefined : (form.getFieldValue('isEventSubscription') as boolean | undefined),
        })
        feedback.success(t('wiki.source.createSuccess'))
      }
      setModalOpen(false)
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSubmitting(false)
    }
  }

  const handleSync = async (record: WikiSourceItem) => {
    if (!record.sourceId) return
    setSyncingId(record.sourceId)
    try {
      const res = await syncWikiSource(wikiId, record.sourceId, false)
      feedback.success(
        t('wiki.source.syncSuccess', {
          created: res.created ?? 0,
          updated: res.updated ?? 0,
          unchanged: res.unchanged ?? 0,
          failed: res.failed ?? 0,
        }),
      )
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSyncingId(null)
    }
  }

  const handleDelete = async (record: WikiSourceItem) => {
    if (!record.sourceId) return
    try {
      await deleteWikiSource(wikiId, record.sourceId)
      feedback.success(t('wiki.source.deleteSuccess'))
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const handleToggleEnable = async (record: WikiSourceItem, checked: boolean) => {
    if (!record.sourceId) return
    try {
      await updateWikiSource(wikiId, record.sourceId, { isEnable: checked })
      feedback.success(t('wiki.source.saveSuccess'))
      void load()
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }

  const loadDocs = useCallback(
    async (record: WikiSourceItem, page = 1, size = docPageSize) => {
      if (!record.sourceId) return
      setDocLoading(true)
      try {
        const res = await getWikiSourceDocuments(wikiId, record.sourceId, { pageNo: page, pageSize: size })
        setDocs(res.items ?? [])
        setDocTotal(res.total ?? 0)
      } catch {
        // 错误已由全局请求中间件统一提示
      } finally {
        setDocLoading(false)
      }
    },
    [wikiId, docPageSize],
  )

  const openDocs = (record: WikiSourceItem) => {
    setDocSource(record)
    setDocPageNo(1)
    setDocDrawerOpen(true)
    void loadDocs(record, 1, docPageSize)
  }

  const syncStatusTag = (status?: WikiSourceSyncStatus | null) => {
    switch (status) {
      case WIKI_SOURCE_SYNC_SUCCESS:
        return <Tag color="green">{t('wiki.source.syncStatusSuccess')}</Tag>
      case WIKI_SOURCE_SYNC_FAILED:
        return <Tag color="red">{t('wiki.source.syncStatusFailed')}</Tag>
      case WIKI_SOURCE_SYNC_SYNCING:
        return <Tag color="blue" icon={<SyncOutlined spin />}>{t('wiki.source.syncStatusSyncing')}</Tag>
      case WIKI_SOURCE_SYNC_NONE:
      default:
        return <Tag>{t('wiki.source.syncStatusNone')}</Tag>
    }
  }

  const docStatusTag = (status?: number | null) => {
    switch (status) {
      case WIKI_SOURCE_DOC_SYNCED:
        return <Tag color="green">{t('wiki.source.docStatusSynced')}</Tag>
      case WIKI_SOURCE_DOC_FAILED:
        return <Tag color="red">{t('wiki.source.docStatusFailed')}</Tag>
      case WIKI_SOURCE_DOC_PENDING:
      default:
        return <Tag color="orange">{t('wiki.source.docStatusPending')}</Tag>
    }
  }

  const columns: TableColumnsType<WikiSourceItem> = useMemo(
    () => [
      {
        title: t('wiki.source.colName'),
        dataIndex: 'name',
        key: 'name',
        width: 220,
        render: (text: string, record) => (
          <Space>
            {record.sourceType === WIKI_SOURCE_TYPE_CRAWLER ? (
              <GlobalOutlined style={{ color: 'var(--color-primary)' }} />
            ) : (
              <FileTextOutlined style={{ color: 'var(--color-primary)' }} />
            )}
            <Tooltip title={record.description || undefined}>
              <span>{text || '-'}</span>
            </Tooltip>
          </Space>
        ),
      },
      {
        title: t('wiki.source.colType'),
        dataIndex: 'sourceType',
        key: 'sourceType',
        width: 120,
        render: (value: WikiSourceType) => (
          <Tag color={value === WIKI_SOURCE_TYPE_CRAWLER ? 'geekblue' : 'blue'}>
            {value === WIKI_SOURCE_TYPE_CRAWLER ? t('wiki.source.typeCrawler') : t('wiki.source.typeFeishu')}
          </Tag>
        ),
      },
      {
        title: t('wiki.source.colTarget'),
        key: 'target',
        ellipsis: true,
        render: (_, record) =>
          record.sourceType === WIKI_SOURCE_TYPE_CRAWLER ? (
            <Tooltip title={record.crawler?.startUrl || undefined}>
              <span style={{ whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>{record.crawler?.startUrl || '-'}</span>
            </Tooltip>
          ) : (
            <Tooltip title={record.feishu?.nodeToken || undefined}>
              <span>{record.feishuAppName || record.feishu?.nodeToken || '-'}</span>
            </Tooltip>
          ),
      },
      {
        title: t('wiki.source.colInterval'),
        key: 'interval',
        width: 110,
        render: (_, record) =>
          record.sourceType === WIKI_SOURCE_TYPE_CRAWLER ? (
            <Text type="secondary">{t('wiki.source.intervalValue', { seconds: record.crawler?.requestIntervalSeconds ?? 1 })}</Text>
          ) : (
            <Text type="secondary">-</Text>
          ),
      },
      {
        title: t('wiki.source.colCron'),
        dataIndex: 'cron',
        key: 'cron',
        width: 140,
        render: (value: string | null | undefined) =>
          value?.trim() ? (
            <Tooltip title={t('wiki.source.cronTooltip')}>
              <Space size={4}>
                <ClockCircleOutlined />
                <Text code style={{ fontSize: 12 }}>{value.trim()}</Text>
              </Space>
            </Tooltip>
          ) : (
            <Text type="secondary">{t('wiki.source.cronOff')}</Text>
          ),
      },
      {
        title: t('wiki.source.colDocumentCount'),
        dataIndex: 'documentCount',
        key: 'documentCount',
        width: 110,
        align: 'center',
        render: (value: number | null | undefined, record) => (
          <AntLink onClick={() => openDocs(record)}>{value ?? 0}</AntLink>
        ),
      },
      {
        title: t('wiki.source.colLastSync'),
        key: 'lastSync',
        width: 200,
        render: (_, record) => (
          <Space direction="vertical" size={2}>
            <Space size={4}>
              {syncStatusTag(record.lastSyncStatus)}
              <Text type="secondary" style={{ fontSize: 12, whiteSpace: 'nowrap' }}>
                {formatDateTime(record.lastSyncTime)}
              </Text>
            </Space>
            {record.lastSyncMessage && (
              <Tooltip title={record.lastSyncMessage}>
                <Text type="secondary" style={{ fontSize: 12, maxWidth: 180, display: 'inline-block', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                  {record.lastSyncMessage}
                </Text>
              </Tooltip>
            )}
          </Space>
        ),
      },
      {
        title: t('wiki.source.colEnable'),
        dataIndex: 'isEnable',
        key: 'isEnable',
        width: 90,
        align: 'center',
        render: (value: boolean | null | undefined, record) => (
          <Switch
            size="small"
            checked={value ?? false}
            disabled={!isAdminPlus}
            aria-label={t('wiki.source.colEnable')}
            onChange={(checked) => void handleToggleEnable(record, checked)}
          />
        ),
      },
      {
        title: t('wiki.source.colActions'),
        key: 'actions',
        width: 150,
        fixed: 'right',
        render: (_, record) => (
          <Space size={0}>
            <Tooltip title={t('wiki.source.syncNow')}>
              <Button
                type="text"
                size="small"
                icon={<SyncOutlined spin={syncingId === record.sourceId} />}
                aria-label={t('wiki.source.syncNow')}
                disabled={!isAdminPlus || syncingId === record.sourceId}
                onClick={() => void handleSync(record)}
              />
            </Tooltip>
            <Tooltip title={t('wiki.source.documents')}>
              <Button
                type="text"
                size="small"
                icon={<FileTextOutlined />}
                aria-label={t('wiki.source.documents')}
                onClick={() => openDocs(record)}
              />
            </Tooltip>
            <Tooltip title={t('wiki.edit')}>
              <Button
                type="text"
                size="small"
                icon={<EditOutlined />}
                aria-label={t('wiki.edit')}
                disabled={!isAdminPlus}
                onClick={() => openEdit(record)}
              />
            </Tooltip>
            <Popconfirm
              title={t('wiki.source.deleteConfirm')}
              description={t('wiki.source.deleteHint')}
              okButtonProps={{ danger: true }}
              onConfirm={() => void handleDelete(record)}
              disabled={!isAdminPlus}
            >
              <Tooltip title={t('wiki.delete')}>
                <Button type="text" size="small" danger icon={<DeleteOutlined />} aria-label={t('wiki.delete')} disabled={!isAdminPlus} />
              </Tooltip>
            </Popconfirm>
          </Space>
        ),
      },
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, isAdminPlus, syncingId],
  )

  const docColumns: TableColumnsType<WikiSourceDocumentItem> = useMemo(
    () => [
      {
        title: t('wiki.source.docColTitle'),
        dataIndex: 'externalTitle',
        key: 'externalTitle',
        ellipsis: true,
        render: (value: string | null | undefined, record) => value || record.fileName || record.externalKey || '-',
      },
      {
        title: t('wiki.source.docColFileName'),
        dataIndex: 'fileName',
        key: 'fileName',
        width: 200,
        ellipsis: true,
        render: (value: string | null | undefined) => value || '-',
      },
      {
        title: t('wiki.source.docColPath'),
        dataIndex: 'externalPath',
        key: 'externalPath',
        ellipsis: true,
        render: (value: string | null | undefined) => (value ? <Text code style={{ fontSize: 12 }}>{value}</Text> : '-'),
      },
      {
        title: t('wiki.source.docColStatus'),
        dataIndex: 'status',
        key: 'status',
        width: 100,
        align: 'center',
        render: (value: number | null | undefined, record) => (
          <Tooltip title={record.lastError || undefined}>{docStatusTag(value)}</Tooltip>
        ),
      },
      {
        title: t('wiki.source.docColLastSync'),
        dataIndex: 'lastSyncTime',
        key: 'lastSyncTime',
        width: 160,
        render: (value: string | null | undefined) => <span style={{ whiteSpace: 'nowrap' }}>{formatDateTime(value)}</span>,
      },
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t],
  )

  return (
    <>
      <DataTable<WikiSourceItem>
        rowKey={(record) => record.sourceId ?? ''}
        columns={columns}
        dataSource={items}
        loading={loading}
        scroll={{ x: 1250 }}
        toolbar={
          <Space wrap>
            <Button
              type="primary"
              icon={<PlusOutlined />}
              disabled={!isAdminPlus}
              onClick={() => openCreate(WIKI_SOURCE_TYPE_FEISHU)}
            >
              {t('wiki.source.createFeishu')}
            </Button>
            <Button icon={<GlobalOutlined />} disabled={!isAdminPlus} onClick={() => openCreate(WIKI_SOURCE_TYPE_CRAWLER)}>
              {t('wiki.source.createCrawler')}
            </Button>
            <Button icon={<ReloadOutlined />} onClick={() => void load()} loading={loading}>
              {t('ds.table.refresh')}
            </Button>
            {!isAdminPlus && <Text type="secondary">{t('wiki.source.readonlyHint')}</Text>}
          </Space>
        }
        pagination={false}
      />

      {/* 新建 / 编辑弹窗：类型由入口按钮（新建）或记录（编辑）决定，弹窗内不再切换类型 */}
      <Modal
        title={
          editing
            ? t('wiki.source.editTitle')
            : activeType === WIKI_SOURCE_TYPE_CRAWLER
              ? t('wiki.source.createCrawler')
              : t('wiki.source.createFeishu')
        }
        open={modalOpen}
        onCancel={() => setModalOpen(false)}
        onOk={() => void handleSubmit()}
        okText={t('wiki.save')}
        cancelText={t('wiki.cancel')}
        okButtonProps={{ 'aria-label': t('wiki.save') }}
        confirmLoading={submitting}
        maskClosable={false}
        destroyOnHidden
        width={640}
      >
        <Form form={form} layout="vertical">
          {editing ? (
            <Form.Item label={t('wiki.source.sourceTypeLabel')} style={{ marginBottom: spacing.md }}>
              <Tag color={activeType === WIKI_SOURCE_TYPE_CRAWLER ? 'geekblue' : 'blue'}>
                {activeType === WIKI_SOURCE_TYPE_CRAWLER ? t('wiki.source.typeCrawler') : t('wiki.source.typeFeishu')}
              </Tag>
            </Form.Item>
          ) : (
            <Form.Item style={{ marginBottom: spacing.md }}>
              <Text type="secondary" style={{ fontSize: 12 }}>
                {activeType === WIKI_SOURCE_TYPE_CRAWLER ? t('wiki.source.typeCrawlerHint') : t('wiki.source.typeFeishuHint')}
              </Text>
            </Form.Item>
          )}
          <Form.Item
            name="name"
            label={t('wiki.source.name')}
            rules={[
              { required: true, message: t('wiki.source.nameRequired') },
              { max: 50, message: t('wiki.source.nameInvalid') },
            ]}
          >
            <Input placeholder={t('wiki.source.namePlaceholder')} maxLength={50} disabled={Boolean(editing)} />
          </Form.Item>
          <Form.Item name="description" label={t('wiki.desc')} rules={[{ max: 255 }]}>
            <Input.TextArea placeholder={t('wiki.descPlaceholder')} maxLength={255} rows={2} />
          </Form.Item>

          {activeType === WIKI_SOURCE_TYPE_CRAWLER ? (
            <WikiSourceCrawlerFormFields form={form} />
          ) : (
            <WikiSourceFeishuFields editing={Boolean(editing)} />
          )}

          <Form.Item name="isEnable" valuePropName="checked" style={{ marginBottom: spacing.sm }}>
            <Switch checkedChildren={t('wiki.source.enableOn')} unCheckedChildren={t('wiki.source.enableOff')} />
          </Form.Item>

          <Form.Item
            label={t('wiki.source.cronLabel')}
            extra={t('wiki.source.cronExtra')}
            style={{ marginBottom: spacing.sm }}
          >
            <Space wrap>
              <Switch checked={cronEnabled} onChange={setCronEnabled} />
              {cronEnabled && (
                <>
                  <Form.Item
                    name="cron"
                    noStyle
                    rules={[
                      { required: true, message: t('wiki.source.cronRequired') },
                      {
                        validator: (_, value) => {
                          const text = String(value ?? '').trim()
                          if (!text) return Promise.resolve()
                          const fields = text.split(/\s+/)
                          if (fields.length !== 5 && fields.length !== 6) {
                            return Promise.reject(new Error(t('wiki.source.cronInvalid')))
                          }
                          return /^[\d*/\-,?LW#\s]+$/.test(text)
                            ? Promise.resolve()
                            : Promise.reject(new Error(t('wiki.source.cronInvalid')))
                        },
                      },
                    ]}
                  >
                    <Input style={{ width: 200 }} placeholder="0 2 * * *" maxLength={64} />
                  </Form.Item>
                  <Text type="secondary" style={{ fontSize: 12 }}>
                    {t('wiki.source.cronPresets')}
                  </Text>
                  {CRON_PRESETS.map((preset) => (
                    <AntLink key={preset.key} style={{ fontSize: 12 }} onClick={() => form.setFieldValue('cron', preset.value)}>
                      {t(`wiki.source.cronPreset.${preset.key}`)}
                    </AntLink>
                  ))}
                </>
              )}
            </Space>
          </Form.Item>

          <WikiSourceWorkflowFields />
        </Form>
      </Modal>

      {/* 已同步文档抽屉 */}
      <Drawer
        title={t('wiki.source.documentsTitle', { name: docSource?.name ?? '' })}
        open={docDrawerOpen}
        onClose={() => setDocDrawerOpen(false)}
        width={860}
        destroyOnHidden
      >
        <DataTable<WikiSourceDocumentItem>
          rowKey={(record) => `${record.externalKey ?? ''}-${record.documentId ?? ''}`}
          columns={docColumns}
          dataSource={docs}
          loading={docLoading}
          scroll={{ x: 860 }}
          onRefresh={() => docSource && void loadDocs(docSource, docPageNo, docPageSize)}
          refreshLoading={docLoading}
          pagination={{
            current: docPageNo,
            pageSize: docPageSize,
            total: docTotal,
            onChange: (page, size) => {
              setDocPageNo(page)
              setDocPageSize(size)
              if (docSource) void loadDocs(docSource, page, size)
            },
          }}
        />
      </Drawer>
    </>
  )
}

/**
 * 飞书文档源字段：沿用原有「选择已有连接 / 新建连接」两种绑定方式与节点参数。
 * 抽到独立组件是为了让爬虫源与飞书源的字段互不干扰，避免隐藏字段被一并校验。
 */function WikiSourceFeishuFields({ editing }: { editing: boolean }) {
  const { t } = useTranslation()
  return (
    <>
      <Form.Item label={t('wiki.source.feishuHint')} style={{ marginBottom: spacing.sm }}>
        <Text type="secondary" style={{ fontSize: 12 }}>{t('wiki.source.feishuHintText')}</Text>
      </Form.Item>
      <Form.Item
        name={'nodeToken' as never}
        label={t('wiki.source.nodeToken')}
        rules={[{ required: !editing, message: t('wiki.source.nodeTokenRequired') }, { max: 128 }]}
      >
        <Input placeholder={t('wiki.source.nodeTokenPlaceholder')} maxLength={128} />
      </Form.Item>
      <Form.Item
        name={'feishuAppId' as never}
        label={t('wiki.source.feishuApp')}
        extra={t('wiki.source.feishuAppExtra')}
      >
        <Input placeholder={t('wiki.source.feishuAppPlaceholder')} maxLength={64} />
      </Form.Item>
      <Form.Item name={'newAppName' as never} label={t('wiki.source.newAppName')} rules={[{ max: 50 }]}>
        <Input placeholder={t('wiki.source.newAppNamePlaceholder')} maxLength={50} />
      </Form.Item>
      <Form.Item name={'newAppId' as never} label={t('wiki.source.newAppId')} rules={[{ max: 64 }]}>
        <Input placeholder="cli_xxxxxxxx" maxLength={64} />
      </Form.Item>
      <Form.Item name={'newAppSecret' as never} label={t('wiki.source.newAppSecret')} rules={[{ max: 128 }]}>
        <Input.Password placeholder="AppSecret" maxLength={128} />
      </Form.Item>
      <Form.Item name={'newAppDomain' as never} label={t('wiki.source.newAppDomain')} extra={t('wiki.source.newAppDomainExtra')} rules={[{ max: 100 }]}>
        <Input placeholder="https://open.feishu.cn" maxLength={100} />
      </Form.Item>
      <Form.Item name={'includeSubNodes' as never} valuePropName="checked" label={t('wiki.source.includeSubNodes')} initialValue>
        <Switch />
      </Form.Item>
      <Form.Item name={'feishuMaxDepth' as never} label={t('wiki.source.maxDepth')} extra={t('wiki.source.maxDepthHint')}>
        <Input type="number" min={0} max={10} />
      </Form.Item>
      <Form.Item name={'maxDocuments' as never} label={t('wiki.source.maxDocuments')} extra={t('wiki.source.maxDocumentsHint')}>
        <Input type="number" min={0} max={1000} />
      </Form.Item>
      <Form.Item name={'isEventSubscription' as never} valuePropName="checked" label={t('wiki.source.eventSubscription')} extra={t('wiki.source.eventSubscriptionHint')}>
        <Switch />
      </Form.Item>
    </>
  )
}
