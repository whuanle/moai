import { useCallback, useEffect, useState } from 'react'
import { pluginApi, type PluginStatsData } from '@/api/plugin'
import { PluginStatsView } from './components/PluginStatsView'

/**
 * 插件统计页签（管理端）：全团队汇总口径（系统侧 + 全部团队自有插件），
 * 调用量 TOP 名称对团队自有插件附加团队名 `{名称}-({团队名})`.
 */
export function PluginStatsPanel() {
  const [data, setData] = useState<PluginStatsData | null>(null)
  const [loading, setLoading] = useState(true)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      setData(await pluginApi.getPluginStats())
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    void load()
  }, [load])

  return <PluginStatsView data={data} loading={loading} showTeamName />
}
