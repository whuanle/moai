// 秘塔搜索工具集插件「桩服务」端到端
// 做法：本地起一个秘塔 API 桩服务 → 用 MoAI__Metaso__Endpoint 启动一个独立后端 →
//       创建 metaso_search / metaso_reader 实例并运行 → 断言「动态插件实例 → 注册表模板 → Refit 客户端 →
//       桩服务 → 响应解析」整条链路，无需真实 API Key、不消耗秘塔点数。
// 桩服务按请求体字段区分端点：含 q 的是搜索（/api/v1/search），含 url 的是阅读（/api/v1/reader，
// 按 Accept 头区分 markdown 纯文本与 JSON 结构化两种响应形态）。
// 前置：dotnet build src/MoAI/MoAI.csproj -o .builds/metaso（bin/Debug 常被常驻后端锁 DLL，独立输出绕开）。
// 用法：node local-dev/metaso-search-e2e.mjs
// 环境变量：MSE_BACKEND_PORT（默认 5203）｜MSE_MOCK_PORT（默认 5202）｜MSE_KEEP_BACKEND=1 保留后端供排查
import { createServer } from 'node:http'
import { spawn } from 'node:child_process'
import crypto from 'node:crypto'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const REPO_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const BACKEND_PORT = Number(process.env.MSE_BACKEND_PORT ?? 5203)
const MOCK_PORT = Number(process.env.MSE_MOCK_PORT ?? 5202)
const BASE = `http://127.0.0.1:${BACKEND_PORT}`
const GOOD_KEY = 'mk-mock-ok'

let PASS = 0, FAIL = 0
const check = (name, cond, detail = '') => {
  if (cond) { PASS++; console.log(`PASS | ${name}`) }
  else { FAIL++; console.log(`FAIL | ${name} ${detail ? '— ' + detail : ''}`) }
}

// ---------------- 秘塔桩服务：按官方 API v1 报文形态回放 ----------------
// 搜索响应为裸载荷（无 code 信封），按 scope 分集合返回；错误以非 2xx HTTP + {message} 表达。
const WEBPAGE_RESPONSE = {
  webpages: [
    {
      title: '量子计算正在走向实用化：2026 年度进展综述',
      link: 'https://example.com/quantum-2026',
      score: '0.98',
      snippet: '量子纠错与逻辑量子比特规模在这一年取得关键突破。',
      position: 1,
      date: '2026-09-30',
      rawContent: '# 量子计算年度综述\n\n逻辑量子比特数量翻倍……',
    },
    {
      title: '量子比特是什么？入门科普',
      link: 'https://example.com/qubit-intro',
      snippet: '量子比特是量子计算的基本信息单元。',
      position: 2,
    },
  ],
  total: 2,
  credits: 1,
}

const SUMMARY_RESPONSE = {
  ...WEBPAGE_RESPONSE,
  summary: '综合来看，量子计算在纠错、规模化和应用落地三个方向都有实质进展。',
}

const DOCUMENT_RESPONSE = {
  documents: [
    { title: '量子计算白皮书（第三届）', link: 'https://wenku.example.com/quantum-whitepaper', snippet: '全文共 68 页，梳理产业化路线。', position: 1 },
  ],
  total: 1,
  credits: 1,
}

const IMAGE_RESPONSE = {
  images: [
    { title: '量子计算机结构图', link: 'https://img.example.com/quantum-chip.png', thumbnail: 'https://img.example.com/quantum-chip-thumb.png' },
  ],
  total: 1,
  credits: 1,
}

const READER_JSON_RESPONSE = {
  title: '量子纠错首次跨越阈值',
  url: 'https://example.com/qec-threshold',
  author: '量子前沿编辑部',
  date: '2026-09-01',
  markdown: '# 量子纠错首次跨越阈值\n\n谷歌与国内团队先后宣布……',
  credits: 2,
}

const READER_MARKDOWN = '# 量子纠错首次跨越阈值\n\n谷歌与国内团队先后宣布……'

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
      const pathName = new URL(req.url ?? '/', 'http://127.0.0.1').pathname
      lastMockRequest = { path: pathName, auth: req.headers['authorization'] ?? '', accept: req.headers['accept'] ?? '', headers: req.headers, body: parsed }

      const send = (code, obj, contentType = 'application/json') => {
        res.writeHead(code, { 'Content-Type': contentType })
        res.end(typeof obj === 'string' ? obj : JSON.stringify(obj))
      }
      const authorized = req.headers['authorization'] === `Bearer ${GOOD_KEY}`

      // 搜索（POST /api/v1/search，请求体含 q）
      if (parsed && parsed.q !== undefined) {
        mockHits.search++
        if (!authorized) return send(401, { message: 'Invalid API key' })
        if (parsed.q === 'quota_exhausted') return send(429, { message: 'quota exceeded' })
        if (parsed.scope === 'document') return send(200, DOCUMENT_RESPONSE)
        if (parsed.scope === 'image') return send(200, IMAGE_RESPONSE)
        return send(200, parsed.includeSummary ? SUMMARY_RESPONSE : WEBPAGE_RESPONSE)
      }

      // 阅读（POST /api/v1/reader，请求体含 url）
      if (parsed && parsed.url !== undefined) {
        mockHits.reader++
        if (!authorized) return send(401, { message: 'Invalid API key' })
        if (parsed.url.startsWith('https://quota.')) return send(429, { message: 'quota exceeded' })
        if ((req.headers['accept'] ?? '').includes('application/json')) return send(200, READER_JSON_RESPONSE)
        return send(200, READER_MARKDOWN, 'text/plain; charset=utf-8')
      }

      return send(404, { message: `no mock for ${pathName}` })
    })
  })
  return new Promise((resolve) => server.listen(MOCK_PORT, '127.0.0.1', () => resolve(server)))
}

function startBackend() {
  // bin/Debug 常被常驻后端/VS 锁定，改从独立输出目录 .builds/metaso 启动；
  // cwd 指向 src/MoAI，使 content root 能读到 appsettings.Development.json（基础设施连接）。
  const dll = path.join(REPO_ROOT, '.builds', 'metaso', 'MoAI.dll')
  const child = spawn('dotnet', [dll, `--urls=http://127.0.0.1:${BACKEND_PORT}`], {
    cwd: path.join(REPO_ROOT, 'src', 'MoAI'),
    env: {
      ...process.env,
      ASPNETCORE_ENVIRONMENT: 'Development',
      MoAI__Port: String(BACKEND_PORT),
      MoAI__Metaso__Endpoint: `http://127.0.0.1:${MOCK_PORT}`,
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
  console.log(`后端启动中：${BASE}（MoAI__Metaso__Endpoint=http://127.0.0.1:${MOCK_PORT}）`)

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
    const S = `mock_metaso_s_${TS}`
    const R = `mock_metaso_r_${TS}`
    const BAD = `mock_metaso_bad_${TS}`
    const EMPTY = `mock_metaso_empty_${TS}`

    // ---------- 搜索：网页范围，全参数 ----------
    check('创建 metaso_search 示例实例', (await save(admin, { pluginKey: S, templeteKey: 'metaso_search', title: '桩服务 搜索', description: 'mock', classifyId: 0, config: JSON.stringify({ ApiKey: GOOD_KEY }) })).status === 200)
    const s1 = await run(admin, S, JSON.stringify({ Query: '  量子计算最新进展  ', IncludeSummary: true, IncludeRawContent: true, ConciseSnippet: true }))
    check('@DYN-S103a 搜索运行成功', s1.json?.success === true, `${s1.status} ${s1.text.slice(0, 240)}`)
    check('@DYN-S103b 网页结果解析（标题/链接/摘要/日期/得分/序号/原文）', s1.data?.Results?.length === 2 && s1.data.Results[0].Title === '量子计算正在走向实用化：2026 年度进展综述' && s1.data.Results[0].Link === 'https://example.com/quantum-2026' && s1.data.Results[0].Snippet?.includes('量子纠错') && s1.data.Results[0].Date === '2026-09-30' && s1.data.Results[0].Score === '0.98' && s1.data.Results[0].Position === 1 && s1.data.Results[0].RawContent?.includes('逻辑量子比特'), JSON.stringify(s1.data?.Results?.[0] ?? {}))
    check('@DYN-S103c 综合摘要/总数/点数/搜索词回显', s1.data?.Summary?.includes('实质进展') && s1.data?.Total === 2 && s1.data?.Credits === 1 && s1.data?.Query === '量子计算最新进展' && s1.data?.Scope === 'webpage', JSON.stringify({ t: s1.data?.Total, q: s1.data?.Query, s: s1.data?.Scope }))
    check('@DYN-S103d 请求下发：Query 去空格 + 鉴权头 Bearer + 全参数透传 + 默认 webpage', lastMockRequest?.path === '/api/v1/search' && lastMockRequest?.body?.q === '量子计算最新进展' && lastMockRequest?.auth === `Bearer ${GOOD_KEY}` && lastMockRequest?.body?.scope === 'webpage' && lastMockRequest?.body?.size === 10 && lastMockRequest?.body?.includeSummary === true && lastMockRequest?.body?.includeRawContent === true && lastMockRequest?.body?.conciseSnippet === true, JSON.stringify(lastMockRequest?.body ?? {}))

    // ---------- 搜索：scope 大小写归一 + 数量上限钳制 + 图片范围 ----------
    const s2 = await run(admin, S, JSON.stringify({ Query: '量子计算机', Scope: 'IMAGE', Size: 200 }))
    check('@DYN-S103e scope 大小写归一 + Size 钳到 100 + 图片结果进 Images', s2.json?.success === true && s2.data?.Scope === 'image' && s2.data?.Results?.length === 0 && s2.data?.Images?.length === 1 && s2.data.Images[0].Link === 'https://img.example.com/quantum-chip.png' && s2.data.Images[0].Thumbnail === 'https://img.example.com/quantum-chip-thumb.png' && lastMockRequest?.body?.scope === 'image' && lastMockRequest?.body?.size === 100, JSON.stringify({ scope: s2.data?.Scope, imgs: s2.data?.Images, body: lastMockRequest?.body }))

    // ---------- 搜索：文库范围（documents 并入 Results） ----------
    const s3 = await run(admin, S, JSON.stringify({ Query: '量子计算白皮书', Scope: 'document' }))
    check('@DYN-S103f 文库结果解析（标题/链接/摘要/序号并入 Results）', s3.json?.success === true && s3.data?.Scope === 'document' && s3.data?.Results?.length === 1 && s3.data.Results[0].Title === '量子计算白皮书（第三届）' && s3.data.Results[0].Position === 1, JSON.stringify(s3.data?.Results ?? []))

    // ---------- 搜索：失败路径 ----------
    const s4 = await run(admin, S, JSON.stringify({ Query: 'quota_exhausted' }))
    check('@DYN-S105a 上游 429 归一为带状态码与响应体的可读失败', s4.json?.success === false && /HTTP 429/.test(s4.json?.error ?? '') && /quota exceeded/.test(s4.json?.error ?? ''), `${s4.status} ${(s4.json?.error ?? '').slice(0, 200)}`)

    const s5 = await run(admin, S, JSON.stringify({ Query: '量子', Scope: 'news' }))
    check('@DYN-S105b 非法 Scope 校验', s5.json?.success === false && /webpage\/document\/paper\/image\/video\/podcast/.test(s5.json?.error ?? ''), `${s5.status} ${(s5.json?.error ?? '').slice(0, 200)}`)

    const s6 = await run(admin, S, JSON.stringify({ Query: '   ' }))
    check('@DYN-S105c 空搜索词校验', s6.json?.success === false && /Query 不能为空/.test(s6.json?.error ?? ''), `${s6.status} ${(s6.json?.error ?? '').slice(0, 200)}`)

    // ---------- 阅读：markdown 纯文本形态 ----------
    check('创建 metaso_reader 示例实例', (await save(admin, { pluginKey: R, templeteKey: 'metaso_reader', title: '桩服务 网页阅读', description: 'mock', classifyId: 0, config: JSON.stringify({ ApiKey: GOOD_KEY }) })).status === 200)
    const r1 = await run(admin, R, JSON.stringify({ Url: '  https://example.com/qec-threshold  ' }))
    check('@DYN-S104a 阅读运行成功（markdown 形态）', r1.json?.success === true, `${r1.status} ${r1.text.slice(0, 240)}`)
    check('@DYN-S104b markdown 正文解析与地址回显（去空格）', r1.data?.Content?.startsWith('# 量子纠错首次跨越阈值') && r1.data?.Url === 'https://example.com/qec-threshold' && r1.data?.Title == null, JSON.stringify(r1.data ?? {}))
    check('@DYN-S104c 阅读请求下发：Accept text/plain + 鉴权头', lastMockRequest?.path === '/api/v1/reader' && lastMockRequest?.accept === 'text/plain' && lastMockRequest?.body?.url === 'https://example.com/qec-threshold' && lastMockRequest?.auth === `Bearer ${GOOD_KEY}`, JSON.stringify({ accept: lastMockRequest?.accept, body: lastMockRequest?.body }))

    // ---------- 阅读：json 结构化形态 ----------
    const r2 = await run(admin, R, JSON.stringify({ Url: 'https://example.com/qec-threshold', Format: 'json' }))
    check('@DYN-S104d json 形态解析（标题/地址/作者/日期/正文/点数）', r2.json?.success === true && r2.data?.Title === '量子纠错首次跨越阈值' && r2.data?.Author === '量子前沿编辑部' && r2.data?.Date === '2026-09-01' && r2.data?.Content?.includes('先后宣布') && r2.data?.Credits === 2 && lastMockRequest?.accept === 'application/json', JSON.stringify(r2.data ?? {}))

    // ---------- 阅读：非法 Format 回落 markdown ----------
    const r3 = await run(admin, R, JSON.stringify({ Url: 'https://example.com/qec-threshold', Format: 'xml' }))
    check('@DYN-S104e 非法 Format 回落 markdown（Accept text/plain）', r3.json?.success === true && lastMockRequest?.accept === 'text/plain', JSON.stringify({ accept: lastMockRequest?.accept }))

    // ---------- 阅读失败路径 ----------
    const r4 = await run(admin, R, JSON.stringify({ Url: 'not a url' }))
    check('@DYN-S105d 非法 Url 校验', r4.json?.success === false && /http\/https/.test(r4.json?.error ?? ''), `${r4.status} ${(r4.json?.error ?? '').slice(0, 200)}`)

    const r5 = await run(admin, R, JSON.stringify({ Url: 'https://quota.example/exhausted' }))
    check('@DYN-S105e 阅读上游 429 归一', r5.json?.success === false && /HTTP 429/.test(r5.json?.error ?? '') && /quota exceeded/.test(r5.json?.error ?? ''), `${r5.status} ${(r5.json?.error ?? '').slice(0, 200)}`)

    // ---------- 上游错误归一（桩服务对无效 Key 返回 401） ----------
    await save(admin, { pluginKey: BAD, templeteKey: 'metaso_search', title: '桩服务 无效 Key', description: 'mock', classifyId: 0, config: JSON.stringify({ ApiKey: 'mk-e2e-invalid' }) })
    const bad = await run(admin, BAD, JSON.stringify({ Query: '量子计算' }))
    const badErr = bad.json?.error ?? ''
    console.log(`INFO | 无效 Key 失败信息：${badErr.slice(0, 240)}`)
    check('@DYN-S105f 上游 401 归一为可读失败且带响应体', bad.json?.success === false && /HTTP 401/.test(badErr) && /Invalid API key/.test(badErr), `${bad.status} ${badErr.slice(0, 200)}`)

    // ---------- InitAsync：空 ApiKey 校验 ----------
    await save(admin, { pluginKey: EMPTY, templeteKey: 'metaso_reader', title: '桩服务 空 Key', description: 'mock', classifyId: 0, config: JSON.stringify({}) })
    const empty = await run(admin, EMPTY, JSON.stringify({ Url: 'https://example.com/qec-threshold' }))
    check('@DYN-S105g 空 API Key 在 InitAsync 被拦截', empty.json?.success === false && /API Key 不能为空/.test(empty.json?.error ?? ''), `${empty.status} ${(empty.json?.error ?? '').slice(0, 200)}`)

    check('桩服务确被命中（搜索 ≥5 次 + 阅读 ≥4 次）', mockHits.search >= 5 && mockHits.reader >= 4, JSON.stringify(mockHits))

    // ---------- 清理 ----------
    await del(admin, S)
    await del(admin, R)
    await del(admin, BAD)
    await del(admin, EMPTY)

    console.log(`\n=== 秘塔搜索工具集插件桩服务 E2E: PASS ${PASS} / FAIL ${FAIL} ===`)
    if (FAIL > 0) process.exitCode = 1
  } finally {
    if (process.env.MSE_KEEP_BACKEND === '1') console.log(`MSE_KEEP_BACKEND=1，保留后端 pid=${backend.pid}`)
    else await killBackend(backend)
    mock.close()
  }
}

main().catch((e) => { console.error('E2E 异常:', e.message); process.exitCode = 1 })
