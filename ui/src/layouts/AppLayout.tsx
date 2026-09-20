import { useEffect } from 'react'
import { Layout } from 'antd'
import { Outlet, useLocation } from 'react-router'
import { AppSider } from './components/AppSider'
import { useSystemName } from './useSystemLogo'
import { useShellStore } from '@/store/shell'

const { Content } = Layout

// 团队详情及其下属二级页面（团队分区/应用设计/应用对话/知识库/知识图谱/团队提示词编辑）
// 不渲染一级侧边栏：这些页面自带二级导航与返回入口，内容区占满全宽
const SECONDARY_NAV_PATH = /^\/team\/[^/]+/
// 流程设计器 design 分区：URL 即可判定真全屏（去掉全局内边距），与 FastGPT 等编排器一致；
// 流程应用的调试/配置/运行历史分区与 Agent 应用分区同路径，URL 无法区分，由页面经 useShellStore 标记
const FULLSCREEN_PATH = /\/app\/[^/]+\/design\/?$/

export function AppLayout() {
  const location = useLocation()
  const shellFullscreen = useShellStore((s) => s.fullscreen)
  const systemName = useSystemName()
  const hideSider = SECONDARY_NAV_PATH.test(location.pathname)
  const fullscreen = FULLSCREEN_PATH.test(location.pathname) || shellFullscreen

  // 浏览器标签页标题跟随系统设置中的网站名称（侧边栏隐藏的二级页面也保持生效）
  useEffect(() => {
    document.title = systemName
  }, [systemName])

  return (
    <Layout style={{ minHeight: '100vh' }}>
      {!hideSider && <AppSider />}
      <Layout>
        <Content style={{ padding: fullscreen ? 0 : 24 }}>
          <Outlet />
        </Content>
      </Layout>
    </Layout>
  )
}
