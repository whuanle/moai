import { Layout } from 'antd'
import { Outlet, useLocation } from 'react-router'
import { AppSider } from './components/AppSider'

const { Content } = Layout

// 团队详情及其下属二级页面（团队分区/应用设计/应用对话/知识库/知识图谱/团队提示词编辑）
// 不渲染一级侧边栏：这些页面自带二级导航与返回入口，内容区占满全宽
const SECONDARY_NAV_PATH = /^\/team\/[^/]+/
// 流程设计器：画布真全屏（去掉全局内边距），与 FastGPT 等编排器一致
const FULLSCREEN_PATH = /\/app\/[^/]+\/design\/?$/

export function AppLayout() {
  const location = useLocation()
  const hideSider = SECONDARY_NAV_PATH.test(location.pathname)
  const fullscreen = FULLSCREEN_PATH.test(location.pathname)
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
