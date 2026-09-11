// 博查系列插件「桩服务」端到端（场景 @DYN-S16 / @DYN-S21b / @DYN-S22）
// 做法：本地起一个 BoCha API 桩服务 → 用 MoAI__BoCha__Endpoint 启动一个独立后端 →
//       创建 bocha_web_search / bocha_ai_search 实例并运行 → 断言「动态插件实例 → 注册表模板 →
//       Refit 客户端 → 桩服务 → 响应解析」整条链路，无需真实 API Key、不消耗博查额度。
// 前置：宿主已构建（dotnet build src/MoAI/MoAI.csproj）；postgres / redis / rabbitmq 已就绪。
// 用法：node local-dev/bocha-search-e2e.mjs
// 环境变量：BSE_BACKEND_PORT（默认 5199）｜BSE_MOCK_PORT（默认 5198）｜BSE_KEEP_BACKEND=1 保留后端供排查
import { createServer } from 'node:http'
import { spawn } from 'node:child_process'
import crypto from 'node:crypto'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const REPO_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const BACKEND_PORT = Number(process.env.BSE_BACKEND_PORT ?? 5199)
const MOCK_PORT = Number(process.env.BSE_MOCK_PORT ?? 5198)
const BASE = `http://127.0.0.1:${BACKEND_PORT}`
const GOOD_KEY = 'sk-mock-ok'

let PASS = 0, FAIL = 0
const check = (name, cond, detail = '') => {
  if (cond) { PASS++; console.log(`PASS | ${name}`) }
  else { FAIL++; console.log(`FAIL | ${name} ${detail ? '— ' + detail : ''}`) }
}

// ---------------- BoCha 桩服务：返回官方文档中的样例报文 ----------------
const WEB_SEARCH_RESPONSE = {
  code: 200,
  log_id: 'mock-log',
  msg: null,
  data: {
    _type: 'SearchResponse',
    queryContext: { originalQuery: '阿里巴巴2024年的ESG报告' },
    webPages: {
      webSearchUrl: '',
      totalEstimatedMatches: 8912791,
      value: [
        {
          id: null,
          name: '阿里巴巴发布2024年ESG报告',
          url: 'https://www.alibabagroup.com/document-1752073403914780672',
          displayUrl: 'https://www.alibabagroup.com/document-1752073403914780672',
          snippet: '阿里巴巴集团发布《2024财年环境、社会和治理（ESG）报告》',
          summary: '报告显示，阿里巴巴扎实推进减碳举措。',
          siteName: '阿里巴巴集团',
          siteIcon: 'https://th.bochaai.com/favicon?domain_url=a',
          datePublished: '2024-07-22T00:00:00+08:00',
          dateLastCrawled: '2024-07-22T00:00:00Z',
        },
        {
          id: null,
          name: '阿里巴巴ESG报告：2024财年优酷无障碍剧场播放次数近百万',
          url: 'https://t.m.youth.cn/x.htm',
          snippet: '阿里巴巴2024年ESG报告截图',
          siteName: '中国青年网',
          datePublished: '2024-07-22T18:48:00+08:00',
        },
      ],
      someResultsRemoved: true,
    },
    images: {
      value: [
        {
          name: '中国天气图片',
          thumbnailUrl: 'http://dayu-img.uc.cn/a.jpg',
          contentUrl: 'http://dayu-img.uc.cn/a.jpg',
          hostPageUrl: 'https://mparticle.uc.cn/x',
          width: 553,
          height: 311,
        },
      ],
    },
    videos: null,
  },
}

const AI_SEARCH_RESPONSE = {
  code: 200,
  log_id: 'mock-log',
  conversation_id: 'mock-conv-001',
  messages: [
    {
      role: 'assistant',
      type: 'source',
      content_type: 'webpage',
      content: JSON.stringify({
        webSearchUrl: 'https://bochaai.com/search?q=西瓜的功效与作用',
        value: [
          {
            id: 'https://api.bochaai.com/v1/#WebPages.0',
            name: '西瓜的功效与作用',
            url: 'https://www.example.com/watermelon',
            snippet: '西瓜性寒，味甘，具有清热解暑的功效。',
            summary: '西瓜含有大量水分与多种维生素。',
            siteName: '百科',
            siteIcon: 'https://th.bochaai.com/favicon?domain_url=b',
            datePublished: '2025-06-27T18:00:00+08:00',
          },
          {
            id: 'https://api.bochaai.com/v1/#WebPages.1',
            name: '夏天吃西瓜的注意事项',
            url: 'https://so.html5.qq.com/x',
            snippet: '脾胃虚寒者不宜多食。',
            siteName: '企鹅号',
            datePublished: '2026-08-13T10:03:33+08:00',
          },
        ],
        someResultsRemoved: true,
      }),
    },
    {
      role: 'assistant',
      type: 'source',
      content_type: 'image',
      content: JSON.stringify({
        value: [
          {
            name: '西瓜切面',
            thumbnailUrl: 'https://q3.itc.cn/a.jpeg',
            contentUrl: 'https://q3.itc.cn/a.jpeg',
            hostPageUrl: 'https://m.sohu.com/a/1',
            width: 800,
            height: 600,
          },
        ],
      }),
    },
    // 当前接口对 video 恒返回空对象，应被忽略而不是产出空条目
    { role: 'assistant', type: 'source', content_type: 'video', content: '{}' },
    {
      role: 'assistant',
      type: 'source',
      content_type: 'weather_china_v2',
      content: JSON.stringify([
        {
          id: null,
          name: '北京天气预报_一周天气预报',
          url: 'https://www.weatherol.com.cn/home?id=101010100',
          displayUrl: 'https://www.weatherol.com.cn/home?id=101010100',
          snippet: '# 天气预报：北京天气预报_一周天气预报',
          summary: '# 城市：北京\n# 空气质量：32',
          siteName: '气象在线',
          siteIcon: 'https://th.bochaai.com/favicon?domain_url=c',
          datePublished: '2026-08-13T10:26:42+08:00',
        },
      ]),
    },
    // 官方文档中「流式响应」的网页/图片以裸对象返回（非流式为 value 包装）；
    // 插件固定走非流式，这里额外放一条以覆盖裸对象这一防御性分支。
    {
      role: 'assistant',
      type: 'source',
      content_type: 'webpage',
      content: JSON.stringify({
        id: 'https://api.bing.microsoft.com/api/v7/#WebPages.0',
        name: '裸对象形态网页',
        url: 'https://mp.pdnews.cn/bare',
        snippet: '天空呈蓝色是瑞利散射的结果。',
        siteName: '人民号',
      }),
    },
    {
      role: 'assistant',
      type: 'answer',
      content_type: 'text',
      content: '# 西瓜的功效与作用\n\n西瓜性寒，具有清热解暑、生津止渴的功效。',
    },
    { role: 'assistant', type: 'follow_up', content_type: 'text', content: '西瓜适合哪些人群食用？' },
  ],
}

let lastMockRequest = null
let mockHits = { webSearch: 0, aiSearch: 0 }

const sleep = (ms) => new Promise((r) => setTimeout(r, ms))

function startMock() {
  const server = createServer((req, res) => {
    let body = ''
    req.on('data', (c) => { body += c })
    req.on('end', () => {
      let parsed = null
      try { parsed = JSON.parse(body) } catch { /* 非 JSON */ }
      // 请求行可能是 origin-form（/v1/ai-search）或 absolute-form（http://host/v1/ai-search），统一取 pathname
      const pathName = new URL(req.url ?? '/', 'http://127.0.0.1').pathname
      lastMockRequest = { path: pathName, rawUrl: req.url, auth: req.headers['authorization'] ?? '', body: parsed }

      const send = (code, obj) => {
        res.writeHead(code, { 'Content-Type': 'application/json' })
        res.end(JSON.stringify(obj))
      }

      if (pathName.startsWith('/v1/ai-search')) {
        mockHits.aiSearch++
        if (req.headers['authorization'] !== `Bearer ${GOOD_KEY}`) {
          return send(401, { code: '401', log_id: 'mock-log', message: 'Invalid API KEY' })
        }

        // 与真实接口一致：answer=false 时不返回总结答案与追问问题
        const messages = parsed?.answer === false
          ? AI_SEARCH_RESPONSE.messages.filter((m) => m.type !== 'answer' && m.type !== 'follow_up')
          : AI_SEARCH_RESPONSE.messages
        return send(200, { ...AI_SEARCH_RESPONSE, messages })
      }
      if (pathName.startsWith('/v1/web-search')) {
        mockHits.webSearch++
        if (req.headers['authorization'] !== `Bearer ${GOOD_KEY}`) {
          return send(401, { code: '401', log_id: 'mock-log', message: 'Invalid API KEY' })
        }
        return send(200, WEB_SEARCH_RESPONSE)
      }
      return send(404, { code: '404', message: `no mock for ${pathName}` })
    })
  })
  return new Promise((resolve) => server.listen(MOCK_PORT, '127.0.0.1', () => resolve(server)))
}

function startBackend() {
  const child = spawn('dotnet', ['run', '--project', 'src/MoAI/MoAI.csproj', '--no-build', '--no-restore'], {
    cwd: REPO_ROOT,
    env: {
      ...process.env,
      ASPNETCORE_ENVIRONMENT: 'Development',
      MoAI__Port: String(BACKEND_PORT),
      MoAI__BoCha__Endpoint: `http://127.0.0.1:${MOCK_PORT}`,
    },
    stdio: ['ignore', 'pipe', 'pipe'],
  })
  let log = ''
  child.stdout.on('data', (d) => { log += d })
  child.stderr.on('data', (d) => { log += d })
  child.getLog = () => log
  return child
}

function killBackend(child) {
  return new Promise((resolve) => {
    if (!child || child.exitCode !== null) return resolve()
    spawn('taskkill', ['/pid', String(child.pid), '/T', '/F'], { stdio: 'ignore' })
      .on('close', () => resolve())
  })
}

async function waitReady(child) {
  for (let i = 0; i < 60; i++) {
    if (child.exitCode !== null) throw new Error(`后端进程提前退出（code=${child.exitCode}）：\n${child.getLog().slice(-2000)}`)
    try {
      const r = await fetch(`${BASE}/api/common/serverinfo`, { signal: AbortSignal.timeout(3000) })
      if (r.ok) return true
    } catch { /* 未就绪 */ }
    await sleep(2000)
  }
  throw new Error(`后端 ${BASE} 未在 120s 内就绪：\n${child.getLog().slice(-2000)}`)
}

// ---------------- HTTP 调用 ----------------
async function api(method, p, { token, body } = {}) {
  const headers = {}
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  if (token) headers['Authorization'] = `Bearer ${token}`
  const res = await fetch(BASE + p, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) })
  const text = await res.text()
  let json = null
  try { json = JSON.parse(text) } catch { /* 非 JSON */ }
  return { status: res.status, json, text }
}

const save = (token, body) => api('POST', '/api/ai/plugin/dynamic/save', { token, body })
const del = (token, pluginKey) => api('DELETE', '/api/ai/plugin/dynamic', { token, body: { pluginKey } })
const run = async (token, key, requestJson) => {
  const r = await api('POST', '/api/ai/plugin/run', { token, body: { key, requestJson } })
  let data = null
  try { data = JSON.parse(r.json?.dataJson ?? 'null') } catch { /* 非 JSON */ }
  return { ...r, data }
}

async function main() {
  const mock = await startMock()
  console.log(`桩服务已就绪：http://127.0.0.1:${MOCK_PORT}`)
  const backend = startBackend()
  console.log(`后端启动中：${BASE}（MoAI__BoCha__Endpoint=http://127.0.0.1:${MOCK_PORT}）`)

  try {
    await waitReady(backend)
    console.log('后端已就绪')

    const si = await api('GET', '/api/common/serverinfo')
    const rsaPub = si.json.rsaPublic
    const rsa = (plain) => {
      const key = crypto.createPublicKey({ key: Buffer.from(rsaPub, 'base64'), format: 'der', type: 'spki' })
      return crypto.publicEncrypt({ key, padding: crypto.constants.RSA_PKCS1_PADDING }, Buffer.from(plain, 'utf8')).toString('base64')
    }
    const login = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
    if (login.status !== 200 || !login.json?.accessToken) throw new Error(`admin 登录失败: ${login.status} ${login.text.slice(0, 200)}`)
    const admin = login.json.accessToken

    const TS = Date.now().toString().slice(-8)
    const WS = `mock_ws_${TS}`
    const AI = `mock_ai_${TS}`
    const BAD = `mock_bad_${TS}`

    // ---------- 全网搜索：真实走完 Refit → 桩服务 → 解析 ----------
    check('创建 bocha_web_search 示例实例', (await save(admin, { pluginKey: WS, templeteKey: 'bocha_web_search', title: '桩服务 全网搜索', description: 'mock', classifyId: 0, config: JSON.stringify({ ApiKey: GOOD_KEY }) })).status === 200)
    const ws = await run(admin, WS, JSON.stringify({ Query: '  阿里巴巴2024年的ESG报告  ', Freshness: '', Summary: true, Count: 999 }))
    check('@DYN-S16a 全网搜索运行成功', ws.json?.success === true, `${ws.status} ${ws.text.slice(0, 240)}`)
    console.log(`INFO | 运行响应 dataJson：${(ws.json?.dataJson ?? '').slice(0, 200)}`)
    check('@DYN-S16b 网页结果解析（标题/链接/站点/发布时间）', ws.data?.WebPages?.length === 2 && ws.data.WebPages[0].Name === '阿里巴巴发布2024年ESG报告' && ws.data.WebPages[0].SiteName === '阿里巴巴集团' && ws.data.WebPages[0].DatePublished === '2024-07-22T00:00:00+08:00', JSON.stringify(ws.data?.WebPages?.[0] ?? {}))
    check('@DYN-S16c 摘要与命中数解析', ws.data?.WebPages?.[0]?.Summary === '报告显示，阿里巴巴扎实推进减碳举措。' && ws.data?.TotalEstimatedMatches === 8912791 && ws.data?.SomeResultsRemoved === true, JSON.stringify({ t: ws.data?.TotalEstimatedMatches, r: ws.data?.SomeResultsRemoved }))
    check('@DYN-S16d 图片结果解析', ws.data?.Images?.length === 1 && ws.data.Images[0].Width === 553 && ws.data.Images[0].Height === 311, JSON.stringify(ws.data?.Images ?? []))
    check('@DYN-S16e 参数下发：Query 去空格 + Count 越界 clamp 50 + Freshness 回落 noLimit', lastMockRequest?.body?.query === '阿里巴巴2024年的ESG报告' && lastMockRequest?.body?.count === 50 && lastMockRequest?.body?.freshness === 'noLimit', JSON.stringify(lastMockRequest?.body ?? {}))
    check('@DYN-S16f 鉴权头为 Bearer <配置中的 Key>', lastMockRequest?.auth === `Bearer ${GOOD_KEY}`, lastMockRequest?.auth ?? '')

    // ---------- AI 搜索：答案 / 追问 / 网页 / 图片 / 模态卡 ----------
    check('创建 bocha_ai_search 示例实例', (await save(admin, { pluginKey: AI, templeteKey: 'bocha_ai_search', title: '桩服务 AI 搜索', description: 'mock', classifyId: 0, config: JSON.stringify({ ApiKey: GOOD_KEY }) })).status === 200)
    const ai = await run(admin, AI, JSON.stringify({ Query: '西瓜的功效与作用', Count: 5, Answer: true }))
    check('@DYN-S22a AI 搜索运行成功', ai.json?.success === true, `${ai.status} ${ai.text.slice(0, 240)}`)
    check('@DYN-S22b 会话 ID 透传', ai.data?.ConversationId === 'mock-conv-001', ai.data?.ConversationId ?? 'null')
    check('@DYN-S22c 大模型答案组装（Markdown）', (ai.data?.Answer ?? '').startsWith('# 西瓜的功效与作用') && ai.data.Answer.includes('清热解暑'), (ai.data?.Answer ?? 'null').slice(0, 80))
    check('@DYN-S22d 追问问题组装', ai.data?.FollowUps?.length === 1 && ai.data.FollowUps[0] === '西瓜适合哪些人群食用？', JSON.stringify(ai.data?.FollowUps ?? []))
    check('@DYN-S22e 参考网页展开（value 包装 → 逐条；含裸对象形态）', ai.data?.WebPages?.length === 3 && ai.data.WebPages[0].Name === '西瓜的功效与作用' && ai.data.WebPages[1].SiteName === '企鹅号' && ai.data.WebPages[2].Name === '裸对象形态网页' && ai.data.WebPages[2].SiteName === '人民号', JSON.stringify(ai.data?.WebPages ?? []))
    check('@DYN-S22f 参考网页摘要与发布结果字段', ai.data?.WebPages?.[0]?.Summary === '西瓜含有大量水分与多种维生素。' && ai.data?.WebPages?.[0]?.DatePublished === '2025-06-27T18:00:00+08:00' && ai.data?.SomeResultsRemoved === true, JSON.stringify(ai.data?.WebPages?.[0] ?? {}))
    check('@DYN-S22g 参考图片解析', ai.data?.Images?.length === 1 && ai.data.Images[0].Width === 800, JSON.stringify(ai.data?.Images ?? []))
    check('@DYN-S22h 模态卡按 content_type 归类并解析通用字段', ai.data?.ModelCards?.length === 1 && ai.data.ModelCards[0].Type === 'weather_china_v2' && ai.data.ModelCards[0].SiteName === '气象在线', JSON.stringify(ai.data?.ModelCards ?? []))
    check('@DYN-S22i 空 video 对象不产出条目', !(ai.data?.ModelCards ?? []).some((c) => c.Type === 'video'), JSON.stringify((ai.data?.ModelCards ?? []).map((c) => c.Type)))
    check('@DYN-S22j 非流式请求（插件引擎无法透出 SSE）', lastMockRequest?.body?.stream === false && lastMockRequest?.body?.answer === true, JSON.stringify(lastMockRequest?.body ?? {}))

    // ---------- AI 搜索：关闭大模型回答 + 参数兜底 ----------
    const ai2 = await run(admin, AI, JSON.stringify({ Query: '西瓜', Count: 999, Answer: false, Freshness: '   ', Include: 'qq.com|m.163.com' }))
    check('@DYN-S22k Answer=false 时无答案与追问', ai2.json?.success === true && ai2.data?.Answer === null && (ai2.data?.FollowUps ?? []).length === 0, JSON.stringify({ a: ai2.data?.Answer, f: ai2.data?.FollowUps }))
    check('@DYN-S22l AI 搜索参数兜底：count clamp 50 / freshness→noLimit / include 透传 / answer=false', lastMockRequest?.body?.count === 50 && lastMockRequest?.body?.freshness === 'noLimit' && lastMockRequest?.body?.include === 'qq.com|m.163.com' && lastMockRequest?.body?.answer === false, JSON.stringify(lastMockRequest?.body ?? {}))

    // ---------- 上游错误归一（桩服务对无效 Key 返回 401） ----------
    await save(admin, { pluginKey: BAD, templeteKey: 'bocha_ai_search', title: '桩服务 无效 Key', description: 'mock', classifyId: 0, config: JSON.stringify({ ApiKey: 'sk-e2e-invalid' }) })
    const bad = await run(admin, BAD, JSON.stringify({ Query: '西瓜的功效与作用' }))
    const badErr = bad.json?.error ?? ''
    console.log(`INFO | 无效 Key 失败信息：${badErr.slice(0, 240)}`)
    check('@DYN-S21b 上游 401 归一为可读失败且带响应体', bad.json?.success === false && /HTTP 401/.test(badErr) && /Invalid API KEY/.test(badErr), `${bad.status} ${badErr.slice(0, 200)}`)

    check('桩服务确被命中（web-search + ai-search 各至少 1 次）', mockHits.webSearch >= 1 && mockHits.aiSearch >= 2, JSON.stringify(mockHits))

    // ---------- 清理 ----------
    await del(admin, WS)
    await del(admin, AI)
    await del(admin, BAD)

    console.log(`\n=== 博查插件桩服务 E2E: PASS ${PASS} / FAIL ${FAIL} ===`)
    if (FAIL > 0) process.exitCode = 1
  } finally {
    if (process.env.BSE_KEEP_BACKEND === '1') console.log(`BSE_KEEP_BACKEND=1，保留后端 pid=${backend.pid}`)
    else await killBackend(backend)
    mock.close()
  }
}

main().catch((e) => { console.error('E2E 异常:', e.message); process.exitCode = 1 })
