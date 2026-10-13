import { useCallback, useEffect, useState } from 'react'
import { Button, Popconfirm, Spin, Tag, Typography } from 'antd'
import { DeleteOutlined, LeftOutlined } from '@ant-design/icons'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams } from 'react-router'
import { feedback, Page } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { clearMyAppMemory, deleteMyMemory, getMyMemories, type MyMemoryItem } from '@/api/memory'
import { formatDateTime } from '@/utils/datetime'

const { Text } = Typography

const PAGE_SIZE = 20

/** 记忆类型常量（对齐后端 AgentMemorySettings.Type*） */
const TYPE_EPISODIC = 1
const TYPE_PROCEDURAL = 2

/**
 * 我的记忆管理页（应用内当前用户的长期记忆）：分页查看、单条删除、清空本应用；
 * 记忆只能删除/清空，不支持编辑修改。入口：工作台「长期记忆」分区与对话页设置面板的「管理记忆」。
 */
export function MyMemoryPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const params = useParams<{ teamId: string; appId: string }>()
  const teamId = Number(params.teamId)
  const appId = params.appId ?? ''

  const [loading, setLoading] = useState(true)
  const [items, setItems] = useState<MyMemoryItem[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [memoryEnabled, setMemoryEnabled] = useState(true)
  const [hoverId, setHoverId] = useState<string | null>(null)

  const load = useCallback(async (targetPage: number) => {
    if (!appId) return
    setLoading(true)
    try {
      const res = await getMyMemories(appId, targetPage, PAGE_SIZE)
      setItems(res.items)
      setTotal(res.total)
      setMemoryEnabled(res.memoryEnabled)
      // 删除末页最后一条后回退一页
      if (res.items.length === 0 && targetPage > 1) {
        setPage(targetPage - 1)
        await load(targetPage - 1)
      }
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [appId])

  useEffect(() => {
    void load(page)
  }, [load, page])

  const handleDelete = useCallback(async (memoryId: string) => {
    try {
      await deleteMyMemory(appId, memoryId)
      feedback.success(t('myMemory.deleteSuccess'))
      await load(page)
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }, [appId, load, page, t])

  const handleClear = useCallback(async () => {
    try {
      await clearMyAppMemory(appId)
      feedback.success(t('myMemory.clearSuccess'))
      setPage(1)
      await load(1)
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }, [appId, load, t])

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE))
  const typeName = (type: number) =>
    type === TYPE_EPISODIC ? t('appMemory.typeEpisodic') : type === TYPE_PROCEDURAL ? t('appMemory.typeProcedural') : t('appMemory.typeSemantic')

  return (
    <Page
      breadcrumb={[
        { title: <Link to="/team">{t('team.title')}</Link> },
        { title: <Link to={`/team/${teamId}/apps`}>{t('team.apps')}</Link> },
        { title: t('myMemory.title') },
      ]}
    >
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: spacing.md, gap: spacing.md }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: spacing.sm, minWidth: 0 }}>
          <Button icon={<LeftOutlined />} onClick={() => navigate(-1)} />
          <Typography.Title level={4} style={{ margin: 0 }}>
            {t('myMemory.title')}
          </Typography.Title>
          {!memoryEnabled && <Tag>{t('myMemory.disabledTag')}</Tag>}
        </div>
        <Popconfirm title={t('myMemory.clearConfirm')} onConfirm={() => void handleClear()}>
          <Button danger disabled={total === 0}>
            {t('myMemory.clear')}
          </Button>
        </Popconfirm>
      </div>

      <Text type="secondary">{t('myMemory.hint')}</Text>

      {loading ? (
        <div style={{ padding: spacing.xl, textAlign: 'center' }}>
          <Spin />
        </div>
      ) : items.length === 0 ? (
        <div style={{ padding: `${spacing.xl}px 0`, textAlign: 'center' }}>
          <Text type="secondary">{t('myMemory.empty')}</Text>
        </div>
      ) : (
        <div style={{ marginTop: spacing.md, display: 'flex', flexDirection: 'column' }}>
          {items.map((item) => (
            <div
              key={item.id}
              onMouseEnter={() => setHoverId(String(item.id))}
              onMouseLeave={() => setHoverId(null)}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: spacing.sm,
                padding: `${spacing.sm}px ${spacing.xs}px`,
                borderBottom: '1px solid rgba(0, 0, 0, 0.06)',
              }}
            >
              <Tag style={{ flexShrink: 0 }}>{typeName(item.memoryType ?? 0)}</Tag>
              <Text style={{ flex: 1, minWidth: 0, whiteSpace: 'normal', wordBreak: 'break-word' }}>{item.content}</Text>
              <Text type="secondary" style={{ flexShrink: 0, fontSize: 12 }}>
                {formatDateTime(item.updateTime)}
              </Text>
              {hoverId === String(item.id) && (
                <Popconfirm title={t('myMemory.deleteConfirm')} onConfirm={() => void handleDelete(String(item.id))}>
                  <Button size="small" type="text" danger icon={<DeleteOutlined />} aria-label={t('myMemory.delete')} />
                </Popconfirm>
              )}
            </div>
          ))}
          {totalPages > 1 && (
            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: spacing.xs, padding: `${spacing.md}px 0 0` }}>
              <Button size="small" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
                {t('myMemory.prev')}
              </Button>
              <Text type="secondary" style={{ alignSelf: 'center' }}>
                {page} / {totalPages}
              </Text>
              <Button size="small" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>
                {t('myMemory.next')}
              </Button>
            </div>
          )}
        </div>
      )}
    </Page>
  )
}
