import { createRoot } from 'react-dom/client'
import '@/i18n'
import '@/index.css'
import { AppProviders } from '@/providers/AppProviders'
import { App } from '@/App'

// 注意：不要包 StrictMode。开发模式的双挂载会让 FlowGram 画布的 IoC 容器重复注册
// （Ambiguous match: FlowRendererRegistry），流程设计器依赖 FlowGram，与官方示例一致不加 StrictMode。
createRoot(document.getElementById('root')!).render(
  <AppProviders>
    <App />
  </AppProviders>,
)
