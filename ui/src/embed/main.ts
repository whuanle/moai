import { MoaiWidget, type WidgetOptions } from './widget'

/**
 * 悬浮对话组件 IIFE 入口：扫描携带 data-app-id 的 moai-widget.js script 标签并初始化.
 *
 * 用法：
 * <script src="https://<server>/embed/moai-widget.js"
 *         data-app-id="<appId>"
 *         data-key="<应用接入key，is_auth=true 时必填>"
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
    void new MoaiWidget(options).init()
  }
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', bootstrap)
} else {
  bootstrap()
}
