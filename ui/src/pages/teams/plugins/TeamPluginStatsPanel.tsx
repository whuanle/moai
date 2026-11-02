import { useCallback, useEffect, useState } from 'react'
import type { PluginStatsData } from '@/api/plugin'
import { getTeamPluginStats } from '@/api/team-plugin'
import { PluginStatsView } from '@/pages/plugins/components/PluginStatsView'

interface TeamPluginStatsPanelProps {
  teamId: number
}

/**
 * 团队插件统计页签：仅本团队自有插件的调用统计，名称只显示插件名（不带团队后缀）.
 */
export function TeamPluginStatsPanel({ teamId }: TeamPluginStatsPanelProps) {
  const [data, setData] = useState<PluginStatsData | null>(null)
  const [loading, setLoading] = useState(true)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      setData(await getTeamPluginStats(teamId))
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [teamId])

  useEffect(() => {
    void load()
  }, [load])

  return <PluginStatsView data={data} loading={loading} />
}
