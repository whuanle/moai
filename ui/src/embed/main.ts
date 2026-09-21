import { MoaiWidget, type WidgetOptions } from './widget'

/**
 * 悬浮对话组件 IIFE 入口：扫描携带 data-app-id 的 moai-widget.js script 标签并初始化.
 *
 * script src 与宿主页同源部署（MoAI 站点根 /embed/moai-widget.js），API 地址默认取 script 的源；
 * 前后端分离部署时可用 data-server 显式指向后端地址。
 *
 * 用法：
 * <script src="https://<site>/embed/moai-widget.js"
 *         data-app-id="<appId>"
 *         data-server="<可选，后端地址，默认同 script 源>"
 *         data-key="<应用接入key，is_auth=true 时必填>"
 *         data-external-user-id="<可选>"
 *         data-nickname="<可选>"></script>
 */
function bootstrap(): void {
  const scripts = Array.from(document.querySelectorAll<HTMLScriptElement>('script[src*="moai-widget"]'))
  for (const script of scripts) {
    const appId = script.dataset.appId
    if (!appId) {
      console.warn('[moai-widget] script 缺少 data-app-id，已跳过 / missing data-app-id, skipped')
      continue
    }

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
    new MoaiWidget(options).init().catch((error) => {
      console.error('[moai-widget] 初始化异常 / initialization error', error)
    })
  }
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', bootstrap)
} else {
  bootstrap()
}
