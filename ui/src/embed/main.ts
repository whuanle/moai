import { MoaiWidget, type WidgetOptions } from './widget'

/**
 * 悬浮对话组件入口（IIFE 单文件，构建后全局变量 MoaiWidget 即此默认导出）.
 *
 * 两种接入方式：
 * 1. 标准嵌入（免改造）：script 标签携带 data-* 自动初始化；
 * 2. 自托管改造（需授权应用推荐）：把 moai-widget.js 下载放到业务系统自己的站点（或直接引用），
 *    再调 MoaiWidget.mount(options) 编程挂载，通过 tokenProvider 注入业务后端签发的外部用户
 *    token（业务后端持应用接入 key 调 POST {server}/api/external/token），key 不落前端；
 *    onRequest 请求中间件可改写组件发出的全部 API 请求（加头/代理）.
 */
const api = {
  /** 编程挂载：创建组件并拉取配置初始化，返回实例（可用实例方法进一步控制） */
  mount(options: WidgetOptions): MoaiWidget {
    const widget = new MoaiWidget(options)
    widget.init().catch((error) => {
      console.error('[moai-widget] 初始化异常 / initialization error', error)
    })
    return widget
  },
  MoaiWidget,
}
export default api

/**
 * 标准嵌入自动引导：扫描携带 data-app-id 的 moai-widget.js script 标签并初始化；
 * 无 data-app-id 的引入（自托管 mount 场景）静默跳过，由业务方自行调 MoaiWidget.mount.
 *
 * script src 与宿主页同源部署（MoAI 站点根 /embed/moai-widget.js），API 地址默认取 script 的源；
 * 前后端分离部署时可用 data-server 显式指向后端地址。
 *
 * 用法：
 * <script src="https://<site>/embed/moai-widget.js"
 *         data-app-id="<appId>"
 *         data-server="<可选，后端地址，默认同 script 源>"
 *         data-key="<应用接入key，is_auth=true 时快速验证用；生产建议 tokenProvider>"
 *         data-external-user-id="<可选>"
 *         data-nickname="<可选>"></script>
 */
function bootstrap(): void {
  const scripts = Array.from(document.querySelectorAll<HTMLScriptElement>('script[src*="moai-widget"]'))
  for (const script of scripts) {
    const appId = script.dataset.appId
    if (!appId) continue

    let server = script.dataset.server
    if (!server) {
      try {
        server = new URL(script.src).origin
      } catch {
        console.warn('[moai-widget] 无法解析 script src 源 / cannot resolve script origin')
        continue
      }
    }

    const options: WidgetOptions = {
      appId,
      server: server.replace(/\/+$/, ''),
      accessAppKey: script.dataset.key,
      externalUserId: script.dataset.externalUserId,
      nickname: script.dataset.nickname,
    }
    api.mount(options)
  }
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', bootstrap)
} else {
  bootstrap()
}
