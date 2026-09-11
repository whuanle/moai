import { useCallback, useEffect, useState } from 'react'
import { Tabs } from 'antd'
import { useTranslation } from 'react-i18next'
import { classifyApi, type PluginClassify } from '@/api/classify'
import {
  getTeamPlugins,
  type TeamCustomPluginItem,
  type TeamDynamicPluginItem,
  type TeamPluginItemType,
} from '@/api/team-plugin'
import { useAppStore } from '@/store/app'
import { TeamCustomPluginPanel } from './TeamCustomPluginPanel'
import { TeamDynamicPluginPanel } from './TeamDynamicPluginPanel'

interface TeamPluginsProps {
  teamId: number
}

export function TeamPlugins({ teamId }: TeamPluginsProps) {
  const { t } = useTranslation()
  const isAdmin = useAppStore((state) => state.userInfo?.isAdmin === true)
  const [items, setItems] = useState<TeamPluginItemType[]>([])
  const [loading, setLoading] = useState(true)
  const [canManage, setCanManage] = useState(false)
  const [classifies, setClassifies] = useState<PluginClassify[]>([])

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const data = await getTeamPlugins(teamId)
      setCanManage(data.canManage === true)
      setItems(data.items ?? [])
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [teamId])

  useEffect(() => {
    void load()
  }, [load])

  useEffect(() => {
    if (!isAdmin) return
    classifyApi
      .getPluginClassifies()
      .then((list) => setClassifies(list))
      .catch(() => {
        // 错误已由全局请求中间件统一提示
      })
  }, [isAdmin])

  const customItems = items.filter((i) => i.kind === 'custom') as TeamCustomPluginItem[]
  const dynamicItems = items.filter((i) => i.kind === 'dynamic') as TeamDynamicPluginItem[]

  const tabItems = [
    {
      key: 'custom',
      label: t('plugins.tabCustom'),
      children: (
        <TeamCustomPluginPanel
          teamId={teamId}
          items={customItems}
          loading={loading}
          canManage={canManage}
          classifies={classifies}
          reload={load}
        />
      ),
    },
    {
      key: 'dynamic',
      label: t('plugins.tabDynamic'),
      children: (
        <TeamDynamicPluginPanel
          teamId={teamId}
          items={dynamicItems}
          loading={loading}
          canManage={canManage}
          classifies={classifies}
          reload={load}
        />
      ),
    },
  ]

  return <Tabs items={tabItems} />
}
