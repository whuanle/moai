import { Layout } from 'antd'
import { Outlet } from 'react-router'
import { AppHeader } from './components/AppHeader'

const { Content } = Layout

export function AppLayout() {
  return (
    <Layout style={{ minHeight: '100vh' }}>
      <AppHeader />
      <Content style={{ padding: 24 }}>
        <Outlet />
      </Content>
    </Layout>
  )
}
