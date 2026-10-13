// Jina 搜索/网页阅读插件「桩服务」端到端
// 做法：本地起一个 Jina API 桩服务 → 用 MoAI__Jina__SearchEndpoint / MoAI__Jina__ReaderEndpoint 启动一个独立后端 →
//       创建 jina_search / jina_reader 实例并运行 → 断言「动态插件实例 → 注册表模板 → Refit 客户端 →
//       桩服务 → 响应解析」整条链路，无需真实 API Key、不消耗 Jina 额度。
// 桩服务按请求体字段区分端点：含 q 的是搜索（s.jina.ai），含 url 的是阅读（r.jina.ai）。
// 前置：宿主已构建（dotnet build src/MoAI/MoAI.csproj）；postgres / redis / rabbitmq 已就绪。
// 用法：node local-dev/jina-search-e2e.mjs
// 环境变量：JSE_BACKEND_PORT（默认 5201）｜JSE_MOCK_PORT（默认 5200）｜JSE_KEEP_BACKEND=1 保留后端供排查
import { createServer } from 'node:http'
import { spawn } from 'node:child_process'
import crypto from 'node:crypto'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const REPO_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const BACKEND_PORT = Number(process.env.JSE_BACKEND_PORT ?? 5201)
const MOCK_PORT = Number(process.env.JSE_MOCK_PORT ?? 5200)
const BASE = `http://127.0.0.1:${BACKEND_PORT}`
const GOOD_KEY = 'jina-mock-ok'

let PASS = 0, FAIL = 0
const check = (name, cond, detail = '') => {
  if (cond) { PASS++; console.log(`PASS | ${name}`) }
  else { FAIL++; console.log(`FAIL | ${name} ${detail ? '— ' + detail : ''}`) }
}

// ---------------- Jina 桩服务：按官方文档样例报文回放 ----------------
const SEARCH_RESPONSE = {
  code: 200,
  status: 20000000,
  data: [
    {
      title: 'Jina AI Reader：把网页转成大模型友好格式',
      description: 'Jina Reader 可以抓取任意网页并转换为 Markdown。',
      url: 'https://jina.ai/reader',
      content: '# Jina AI Reader\n\nJina Reader 可以抓取任意网页并转换为 **Markdown**。',
      usage: { tokens: 1200 },
    },
    {
      title: 'reader-lm：从 HTML 到 Markdown 的小型语言模型',
      description: 'reader-lm 是将原始 HTML 转换为清洁 Markdown 的模型系列。',
      url: 'https://jina.ai/models/reader-lm',
      content: '## reader-lm\n\n0.5B 和 1.5B 两个规格。',
      usage: { tokens: 800 },
    },
  ],
  usage: { tokens: 2000 },
}

const READER_RESPONSE = {
  code: 200,
  status: 20000000,
  data: {
    title: 'Reader API · Jina AI',
    description: '把任意网页与 PDF 转换成 LLM 友好的 Markdown。',
    url: 'https://jina.ai/reader',
    content: '# Reader API\n\nConvert any URL to LLM-friendly input.',
    publishTime: '2026-08-01T09:30:00Z',
    links: { 'https://jina.ai/': 'Jina AI', 'https://jina.ai/reader': 'Reader' },
    images: { 'https://jina.ai/og.png': 'Reader OG image' },
    usage: { tokens: 3500 },
  },
}

let lastMockRequest = null
let mockHits = { search: 0, reader: 0 }

const sleep = (ms) => new Promise((r) => setTimeout(r, ms))

function startMock() {
  const server = createServer((req, res) => {
    let body = ''
    req.on('data', (c) => { body += c })
    req.on('end', () => {
      let parsed = null
      try { parsed = JSON.parse(body) } catch { /* 非 JSON */ }
      // 请求行可能是 origin-form（/）或 absolute-form（http://host/），统一取 pathname
      const pathName = new URL(req.url ?? '/', 'http://127.0.0.1').pathname
      lastMockRequest = { path: pathName, auth: req.headers['authorization'] ?? '', headers: req.headers, body: parsed }

      const send = (code, obj) => {
        res.writeHead(code, { 'Content-Type': 'application/json' })
        res.end(JSON.stringify(obj))
      }

      // 搜索（s.jina.ai POST /，请求体含 q）
      if (parsed && parsed.q !== undefined) {
        mockHits.search++
        if (req.headers['authorization'] !== `Bearer ${GOOD_KEY}`) {
          return send(401, { code: 401, status: 40104001, error: true, message: 'Invalid API key' })
        }
        // 特殊搜索词：模拟「HTTP 200 但业务 code 非 200」（额度耗尽）
        if (parsed.q === 'quota_exhausted') {
          return send(200, { code: 429, status: 42904000, error: true, message: 'quota exceeded' })
        }
        return send(200, SEARCH_RESPONSE)
      }

      // 阅读（r.jina.ai POST /，请求体含 url）
      if (parsed && parsed.url !== undefined) {
        mockHits.reader++
        if (req.headers['authorization'] !== `Bearer ${GOOD_KEY}`) {
          return send(401, { code: 401, status: 40104001, error: true, message: 'Invalid API key' })
        }
        if (parsed.url.startsWith('https://quota.')) {
          return send(200, { code: 429, status: 42904000, error: true, message: 'quota exceeded' })
        }
        return send(200, READER_RESPONSE)
      }

      return send(404, { code: 404, message: `no mock for ${pathName}` })
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
      MoAI__Jina__SearchEndpoint: `http://127.0.0.1:${MOCK_PORT}`,
      MoAI__Jina__ReaderEndpoint: `http://127.0.0.1:${MOCK_PORT}`,
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
  console.log(`后端启动中：${BASE}（MoAI__Jina__*__Endpoint=http://127.0.0.1:${MOCK_PORT}）`)

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
    const S = `mock_jina_s_${TS}`
    const R = `mock_jina_r_${TS}`
    const BAD = `mock_jina_bad_${TS}`
    const EMPTY = `mock_jina_empty_${TS}`

    // ---------- 搜索：真实走完 Refit → 桩服务 → 解析 ----------
    check('创建 jina_search 示例实例', (await save(admin, { pluginKey: S, templeteKey: 'jina_search', title: '桩服务 搜索', description: 'mock', classifyId: 0, config: JSON.stringify({ ApiKey: GOOD_KEY }) })).status === 200)
    const s1 = await run(admin, S, JSON.stringify({ Query: '  jina reader 介绍  ', ReturnFormat: 'text', NoCache: true }))
    check('@DYN-S76a 搜索运行成功', s1.json?.success === true, `${s1.status} ${s1.text.slice(0, 240)}`)
    console.log(`INFO | 运行响应 dataJson：${(s1.json?.dataJson ?? '').slice(0, 200)}`)
    check('@DYN-S76b 搜索结果解析（标题/链接/摘要/正文）', s1.data?.Results?.length === 2 && s1.data.Results[0].Title === 'Jina AI Reader：把网页转成大模型友好格式' && s1.data.Results[0].Url === 'https://jina.ai/reader' && s1.data.Results[0].Description?.includes('抓取任意网页') && s1.data.Results[0].Content?.includes('**Markdown**'), JSON.stringify(s1.data?.Results?.[0] ?? {}))
    check('@DYN-S76c token 用量汇总（各条 usage.tokens 求和）', s1.data?.UsageTokens === 2000 && s1.data?.Query === 'jina reader 介绍', JSON.stringify({ u: s1.data?.UsageTokens, q: s1.data?.Query }))
    check('@DYN-S76d 请求下发：Query 去空格 + 鉴权头 Bearer + ReturnFormat/NoCache 头透传', lastMockRequest?.body?.q === 'jina reader 介绍' && lastMockRequest?.auth === `Bearer ${GOOD_KEY}` && lastMockRequest?.headers['x-return-format'] === 'text' && lastMockRequest?.headers['x-no-cache'] === 'true', JSON.stringify({ q: lastMockRequest?.body?.q, h: lastMockRequest?.headers }))

    const s2 = await run(admin, S, JSON.stringify({ Query: 'jina reader' }))
    check('@DYN-S76e ReturnFormat 缺省回落 markdown', s2.json?.success === true && lastMockRequest?.headers['x-return-format'] === 'markdown' && lastMockRequest?.headers['x-no-cache'] === undefined, JSON.stringify(lastMockRequest?.headers ?? {}))

    // ---------- 搜索：HTTP 200 但业务 code 非 200（额度耗尽） ----------
    const s3 = await run(admin, S, JSON.stringify({ Query: 'quota_exhausted' }))
    check('@DYN-S78b 搜索业务错误归一（code=429 带可读信息）', s3.json?.success === false && /code=429/.test(s3.json?.error ?? '') && /quota exceeded/.test(s3.json?.error ?? ''), `${s3.status} ${(s3.json?.error ?? '').slice(0, 200)}`)

    // ---------- 阅读：正文 / 链接 / 图片 / 发布时间 ----------
    check('创建 jina_reader 示例实例', (await save(admin, { pluginKey: R, templeteKey: 'jina_reader', title: '桩服务 网页阅读', description: 'mock', classifyId: 0, config: JSON.stringify({ ApiKey: GOOD_KEY }) })).status === 200)
    const r1 = await run(admin, R, JSON.stringify({ Url: '  https://jina.ai/reader  ', WithLinksSummary: true, WithImagesSummary: true }))
    check('@DYN-S77a 网页阅读运行成功', r1.json?.success === true, `${r1.status} ${r1.text.slice(0, 240)}`)
    check('@DYN-S77b 阅读结果解析（标题/地址/摘要/正文/发布时间）', r1.data?.Title === 'Reader API · Jina AI' && r1.data?.Url === 'https://jina.ai/reader' && r1.data?.Description?.includes('LLM 友好') && r1.data?.Content?.startsWith('# Reader API') && r1.data?.PublishTime === '2026-08-01T09:30:00Z', JSON.stringify(r1.data ?? {}))
    check('@DYN-S77c 链接与图片摘要解析', r1.data?.Links?.['https://jina.ai/'] === 'Jina AI' && r1.data?.Images?.['https://jina.ai/og.png'] === 'Reader OG image', JSON.stringify({ l: r1.data?.Links, i: r1.data?.Images }))
    check('@DYN-S77d 阅读请求下发：Url 去空格 + Accept JSON + 摘要开关头透传 + token 用量', lastMockRequest?.body?.url === 'https://jina.ai/reader' && lastMockRequest?.headers['accept'] === 'application/json' && lastMockRequest?.headers['x-with-links-summary'] === 'true' && lastMockRequest?.headers['x-with-images-summary'] === 'true' && r1.data?.UsageTokens === 3500, JSON.stringify(lastMockRequest?.headers ?? {}))

    const r2 = await run(admin, R, JSON.stringify({ Url: 'https://jina.ai/reader', ReturnFormat: 'html', NoCache: true, Timeout: 30 }))
    check('@DYN-S77e 阅读可选头：ReturnFormat/NoCache/Timeout 透传', r2.json?.success === true && lastMockRequest?.headers['x-return-format'] === 'html' && lastMockRequest?.headers['x-no-cache'] === 'true' && lastMockRequest?.headers['x-timeout'] === '30', JSON.stringify(lastMockRequest?.headers ?? {}))

    // ---------- 阅读失败路径 ----------
    const r3 = await run(admin, R, JSON.stringify({ Url: 'not a url' }))
    check('@DYN-S78a 非法 Url 校验', r3.json?.success === false && /http\/https/.test(r3.json?.error ?? ''), `${r3.status} ${(r3.json?.error ?? '').slice(0, 200)}`)

    const r4 = await run(admin, R, JSON.stringify({ Url: 'https://quota.example/exhausted' }))
    check('@DYN-S78c 阅读业务错误归一（code=429 带可读信息）', r4.json?.success === false && /code=429/.test(r4.json?.error ?? '') && /quota exceeded/.test(r4.json?.error ?? ''), `${r4.status} ${(r4.json?.error ?? '').slice(0, 200)}`)

    // ---------- 上游错误归一（桩服务对无效 Key 返回 401） ----------
    await save(admin, { pluginKey: BAD, templeteKey: 'jina_search', title: '桩服务 无效 Key', description: 'mock', classifyId: 0, config: JSON.stringify({ ApiKey: 'jina-e2e-invalid' }) })
    const bad = await run(admin, BAD, JSON.stringify({ Query: 'jina reader' }))
    const badErr = bad.json?.error ?? ''
    console.log(`INFO | 无效 Key 失败信息：${badErr.slice(0, 240)}`)
    check('@DYN-S78d 上游 401 归一为可读失败且带响应体', bad.json?.success === false && /HTTP 401/.test(badErr) && /Invalid API key/.test(badErr), `${bad.status} ${badErr.slice(0, 200)}`)

    // ---------- InitAsync：空 ApiKey 校验 ----------
    await save(admin, { pluginKey: EMPTY, templeteKey: 'jina_reader', title: '桩服务 空 Key', description: 'mock', classifyId: 0, config: JSON.stringify({}) })
    const empty = await run(admin, EMPTY, JSON.stringify({ Url: 'https://jina.ai/reader' }))
    check('@DYN-S78e 空 API Key 在 InitAsync 被拦截', empty.json?.success === false && /API Key 不能为空/.test(empty.json?.error ?? ''), `${empty.status} ${(empty.json?.error ?? '').slice(0, 200)}`)

    check('桩服务确被命中（搜索 ≥3 次 + 阅读 ≥3 次）', mockHits.search >= 3 && mockHits.reader >= 3, JSON.stringify(mockHits))

    // ---------- 清理 ----------
    await del(admin, S)
    await del(admin, R)
    await del(admin, BAD)
    await del(admin, EMPTY)

    console.log(`\n=== Jina 插件桩服务 E2E: PASS ${PASS} / FAIL ${FAIL} ===`)
    if (FAIL > 0) process.exitCode = 1
  } finally {
    if (process.env.JSE_KEEP_BACKEND === '1') console.log(`JSE_KEEP_BACKEND=1，保留后端 pid=${backend.pid}`)
    else await killBackend(backend)
    mock.close()
  }
}

main().catch((e) => { console.error('E2E 异常:', e.message); process.exitCode = 1 })
