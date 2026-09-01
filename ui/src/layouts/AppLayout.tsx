import { Layout } from 'antd'
import { Outlet } from 'react-router'
import { AppSider } from './components/AppSider'

const { Content } = Layout

export function AppLayout() {
  return (
    <Layout style={{ minHeight: '100vh' }}>
      <AppSider />
      <Layout>
        <Content style={{ padding: 24 }}>
          <Outlet />
        </Content>
      </Layout>
    </Layout>
  )
}
