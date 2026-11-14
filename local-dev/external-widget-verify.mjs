// 悬浮组件访问点端到端验证：真实构建产物（src/MoAI/wwwroot/embed/moai-widget.js）+ jsdom DOM + 真实后端
// 用法：node local-dev/external-widget-verify.mjs [baseUrl]（默认 http://127.0.0.1:5000；前置：后端运行中；依赖 ui/node_modules 的 jsdom；对话走应用配置的真实模型）
// 背景：曾出现产物过期仍调退役接口 /api/external/* 导致嵌入页组件不渲染（访问点 HTML bug），本脚本即回归该链路；
//       2026-11 起覆盖显示形式（悬浮/内嵌）、面板放大、开场白、思考流、停止生成、会话恢复与新对话。
import { readFileSync } from 'node:fs'
import { createRequire } from 'node:module'
const require = createRequire('file:///F:/workspace/moai/ui/package.json')
const { JSDOM, VirtualConsole } = require('jsdom')

const SERVER = process.argv[2] ?? 'http://127.0.0.1:5000'
const APP_ID = '01a1ba18-f85f-727a-bfe9-9f1897ddc1aa'
const bundle = readFileSync('F:/workspace/moai/src/MoAI/wwwroot/embed/moai-widget.js', 'utf8')

// 组件的 console.warn/error 转发到终端，便于排查（jsdom 默认吞掉）
const virtualConsole = new VirtualConsole()
for (const method of ['warn', 'error', 'log']) virtualConsole.on(method, (...args) => console.log(`[widget:${method}]`, ...args))
const dom = new JSDOM('<!DOCTYPE html><html><body></body></html>', { url: 'https://biz.example.com/host-page', pretendToBeVisual: true, runScripts: 'outside-only', virtualConsole })
const { window } = dom
window.fetch = (url, init) => fetch(url, init) // 直连真实后端
window.AbortController = AbortController // jsdom/undici 跨 realm AbortSignal 兼容（仅测试环境需要）
window.AbortSignal = AbortSignal
window.eval(bundle)
console.log('bundle loaded, MoaiWidget.mount =', typeof window.MoaiWidget?.mount)

const assert = (name, cond, detail = '') => { console.log(`${cond ? 'PASS' : 'FAIL'} | ${name}${cond ? '' : ' — ' + detail}`); if (!cond) process.exitCode = 1 }
const sleep = (ms) => new Promise(r => setTimeout(r, ms))

// 0. 访问点新字段（displayMode / openingStatement）
{
  const cfg = await (await fetch(`${SERVER}/api/teamres/app/${APP_ID}/access-point`)).json()
  assert('访问点公开配置含 displayMode', cfg.displayMode === 'floating' || cfg.displayMode === 'inline', `got ${cfg.displayMode}`)
  assert('访问点公开配置含开场白 openingStatement', typeof cfg.openingStatement === 'string' && cfg.openingStatement.length > 0, 'openingStatement 为空')
}

// 1. 悬浮模式：渲染 + 默认展开 + 开场白 + 放大
const widgetApi = window.MoaiWidget.mount({ appId: APP_ID, server: SERVER, externalUserId: 'jsdom-e2e-001', nickname: 'jsdom验证员' })
let root = null
for (let i = 0; i < 50 && !root; i++) { await sleep(100); root = window.document.querySelector('.moai-widget-root') }
assert('悬浮根节点渲染（.moai-widget-root）', !!root)
const shadow = root?.shadowRoot
const panel = shadow?.querySelector('.panel')
assert('面板默认展开（defaultOpen=true → .open）', !!panel?.classList.contains('open'))
assert('悬浮按钮文案 = 在线咨询', shadow?.querySelector('.launcher')?.textContent?.trim() === '在线咨询', `got: ${shadow?.querySelector('.launcher')?.textContent?.trim()}`)
assert('面板标题 = MoAI 测试助手', shadow?.querySelector('.title')?.textContent === 'MoAI 测试助手')
assert('开场白气泡展示（openingStatement）', (shadow?.querySelector('.messages')?.textContent ?? '').includes('GLM-5.3-Flash'), '未找到开场白内容')
const expandBtn = shadow?.querySelector('.expand')
expandBtn?.click()
assert('点击 ⤢ 面板进入近全屏（.expanded）', !!panel?.classList.contains('expanded'))
expandBtn?.click()
assert('再次点击还原（移除 .expanded）', !panel?.classList.contains('expanded'))

// 2. 真实对话：思考流（如有）+ 流式回复 + 完成态（复制按钮）
await sleep(1500)
const ta = shadow?.querySelector('textarea')
const send = () => ta.dispatchEvent(new window.KeyboardEvent('keydown', { key: 'Enter', bubbles: true }))
ta.value = '请只回复四个字：链路正常'
send()
let reply = '', sawReasoning = false, sawDone = false
for (let i = 0; i < 300 && !sawDone; i++) {
  await sleep(300)
  const msgs = [...(shadow?.querySelectorAll('.msg.assistant') ?? [])]
  const last = msgs[msgs.length - 1]
  if (!last) continue
  if (last.querySelector('.reasoning')?.style?.display !== 'none') sawReasoning = true
  reply = (last.querySelector('.msg-body')?.textContent ?? '').trim()
  if (last.classList.contains('done')) sawDone = true
}
assert('GLM-5.3-Flash 流式回复到达（完成态）', sawDone && !!reply, `done=${sawDone} reply=${reply.slice(0, 40)}`)
console.log('     模型回复:', reply.slice(0, 120))
console.log(sawReasoning ? '     思考流: 已出现思考块（reasoning 事件链路通）' : '     思考流: 本轮未见 reasoning 块（渠道未下发思考内容，打字动效兜底）')

// 3. 会话恢复：同身份重新挂载 → localStorage 会话 id 命中 → 历史消息回放
// 消息在流结束后异步落库（实测约 2~6s），先轮询落库再重挂载，避免竞态
window.document.querySelectorAll('.moai-widget-root').forEach((el) => el.remove())
const sessionId = window.localStorage.getItem(`moai-widget-session-${APP_ID}`)
assert('会话 id 已持久化（localStorage）', !!sessionId && /^[0-9a-f-]{36}$/.test(sessionId), `got ${sessionId}`)
{
  const probe = await (await fetch(`${SERVER}/api/teamres/token`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ appId: APP_ID, externalUserId: 'jsdom-e2e-001' }) })).json()
  for (let i = 0; i < 20; i++) {
    const items = (await (await fetch(`${SERVER}/api/teamres/session/${sessionId}/messages`, { headers: { Authorization: `Bearer ${probe.accessToken}` } })).json()).items ?? []
    if (items.length > 0) break
    await sleep(1500)
  }
}
window.MoaiWidget.mount({ appId: APP_ID, server: SERVER, externalUserId: 'jsdom-e2e-001' })
let root2 = null
for (let i = 0; i < 50 && !root2; i++) { await sleep(100); root2 = window.document.querySelector('.moai-widget-root') }
await sleep(2500)
const shadow2 = root2?.shadowRoot
const historyText = (shadow2?.querySelector('.messages')?.textContent ?? '')
assert('重新挂载恢复历史会话（含上轮用户消息）', historyText.includes('链路正常') || historyText.includes('四个字'), historyText.slice(0, 120))

// 4. 新对话按钮：清空 + 重开场白 + 清持久化
shadow2?.querySelector('.new-chat')?.click()
assert('新对话清空消息区并重开场白', (shadow2?.querySelector('.messages')?.textContent ?? '').includes('GLM-5.3-Flash') && !(shadow2?.querySelector('.messages')?.textContent ?? '').includes('链路正常'))
assert('新对话清除持久化会话', window.localStorage.getItem(`moai-widget-session-${APP_ID}`) === null)

// 5. 内嵌小窗模式：渲染进占位容器、无悬浮按钮、面板进文档流
const host = window.document.createElement('div')
host.id = 'moai-inline-demo'
window.document.body.appendChild(host)
window.MoaiWidget.mount({ appId: APP_ID, server: SERVER, mode: 'inline', target: '#moai-inline-demo', externalUserId: 'jsdom-e2e-002' })
let inlineRoot = null
for (let i = 0; i < 50 && !inlineRoot; i++) { await sleep(100); inlineRoot = host.querySelector('.moai-widget-root') }
assert('内嵌模式渲染进占位容器', !!inlineRoot)
const inlineShadow = inlineRoot?.shadowRoot
assert('内嵌模式无悬浮按钮', inlineShadow?.querySelector('.launcher') == null)
assert('内嵌模式 inline-host 类生效', inlineRoot?.classList.contains('inline-host'))
const inlinePanel = inlineShadow?.querySelector('.panel')
inlineShadow?.querySelector('.expand')?.click()
assert('内嵌面板同样支持放大（.expanded）', !!inlinePanel?.classList.contains('expanded'))
inlineShadow?.querySelector('.expand')?.click()

console.log('DONE')
process.exit(process.exitCode ?? 0)
