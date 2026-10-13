// 雅虎财经插件「桩服务」端到端（场景 @DYN-S79 ~ @DYN-S84）
// 做法：本地起一个 Yahoo Finance 数据桩（v8 chart + v1 search）→ 起一个独立后端 →
//       创建 yahoo_finance 实例（BaseUrl 指向桩服务）并运行三模式 → 断言「动态插件实例 → 注册表模板 →
//       Refit 客户端 → 桩服务 → 响应解析」整条链路。数据源地址在实例配置中（无需应用级覆盖），
//       无需真实外网、不受雅虎地域拦截影响。
// 前置：宿主已构建（dotnet build src/MoAI/MoAI.csproj）；postgres / redis / rabbitmq 已就绪。
// 用法：node local-dev/yahoo-finance-e2e.mjs
// 环境变量：YF_BACKEND_PORT（默认 5211）｜YF_MOCK_PORT（默认 5210）｜YF_KEEP_BACKEND=1 保留后端供排查
import { createServer } from 'node:http'
import { spawn } from 'node:child_process'
import crypto from 'node:crypto'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const REPO_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const BACKEND_PORT = Number(process.env.YF_BACKEND_PORT ?? 5211)
const MOCK_PORT = Number(process.env.YF_MOCK_PORT ?? 5210)
const BASE = `http://127.0.0.1:${BACKEND_PORT}`

let PASS = 0, FAIL = 0
const check = (name, cond, detail = '') => {
  if (cond) { PASS++; console.log(`PASS | ${name}`) }
  else { FAIL++; console.log(`FAIL | ${name} ${detail ? '— ' + detail : ''}`) }
}

// ---------------- 桩服务：按雅虎财经官方报文回放 ----------------
// 1728000000 = 2024-10-04T00:00:00Z（整 20000 天）
const HISTORY_COUNT = 35 // > 30，验证「保留最近 30 点」截断
const historyTimestamps = Array.from({ length: HISTORY_COUNT }, (_, i) => 1727568000 + i * 86400)
const historyCloses = historyTimestamps.map((_, i) => (i === 3 ? null : 220 + i * 0.5))

const CHART_AAPL = {
  chart: {
    result: [
      {
        meta: {
          currency: 'USD',
          symbol: 'AAPL',
          exchangeName: 'NMS',
          fullExchangeName: 'NASDAQ',
          instrumentType: 'EQUITY',
          regularMarketPrice: 229.5,
          chartPreviousClose: 227.52,
          regularMarketDayHigh: 231.2,
          regularMarketDayLow: 228.1,
          regularMarketVolume: 51234567,
          fiftyTwoWeekHigh: 237.25,
          fiftyTwoWeekLow: 164.08,
          regularMarketTime: 1728000000,
          longName: 'Apple Inc.',
          shortName: 'Apple Inc.',
        },
        timestamp: historyTimestamps,
        indicators: { quote: [{ close: historyCloses }] },
      },
    ],
    error: null,
  },
}

// 精简 meta：缺日内高低/成交量/52 周/regularMarketTime，验证可选字段容错
const CHART_MSFT = {
  chart: {
    result: [
      {
        meta: {
          currency: 'USD',
          symbol: 'MSFT',
          exchangeName: 'NMS',
          instrumentType: 'EQUITY',
          regularMarketPrice: 421.3,
          chartPreviousClose: 418.5,
          longName: 'Microsoft Corporation',
        },
      },
    ],
    error: null,
  },
}

// 无效代码：上游惯例是 HTTP 200 + chart.error
const CHART_DEAD = {
  chart: { result: null, error: { code: 'Not Found', description: 'No data found, symbol may be delisted' } },
}

const SEARCH_NEWS = {
  explains: [],
  count: 3,
  news: [
    {
      uuid: 'u1',
      title: '苹果发布新款 AI 手机',
      publisher: '新浪科技',
      link: 'https://finance.sina.com.cn/apple-ai',
      providerPublishTime: 1728000000,
      type: 'STORY',
      relatedTickers: ['AAPL', 'MSFT'],
    },
    {
      uuid: 'u2',
      title: '无发布时间的新闻条目',
      publisher: '路透社',
      link: 'https://www.reuters.com/no-time',
      type: 'STORY',
    },
    // 空标题条目应被丢弃，不产出空壳
    { uuid: 'u3', title: '', publisher: '占位社', link: 'https://placeholder.example/a3' },
  ],
}

const SEARCH_QUOTES = {
  explains: [],
  count: 2,
  quotes: [
    { symbol: 'AAPL', shortname: 'Apple Inc.', longname: 'Apple Inc.', exchDisp: 'NASDAQ', exchange: 'NMS', typeDisp: 'Equity', quoteType: 'EQUITY', isYahooFinance: true },
    // 无 shortname：名称回落 longname
    { symbol: 'AAPL34.SA', longname: 'Apple Inc. (Sao Paulo)', exchDisp: 'Sao Paulo', exchange: 'SAO', typeDisp: 'Equity', quoteType: 'EQUITY' },
  ],
}

let lastChart = null // { symbol, ua, range, interval }
let lastSearch = null // { q, quotesCount, newsCount, lang, region, ua }
let mockHits = { chart: 0, search: 0 }

const sleep = (ms) => new Promise((r) => setTimeout(r, ms))

function startMock() {
  const server = createServer((req, res) => {
    const url = new URL(req.url ?? '/', 'http://127.0.0.1')
    const ua = req.headers['user-agent'] ?? ''

    if (url.pathname.startsWith('/v8/finance/chart/')) {
      mockHits.chart++
      const symbol = decodeURIComponent(url.pathname.slice('/v8/finance/chart/'.length))
      lastChart = { symbol, ua, range: url.searchParams.get('range'), interval: url.searchParams.get('interval') }

      const send = (code, obj) => {
        res.writeHead(code, { 'Content-Type': 'application/json' })
        res.end(JSON.stringify(obj))
      }

      if (symbol === 'HTTPFAIL') return send(500, { error: 'upstream exploded' })
      if (symbol === 'DEAD') return send(200, CHART_DEAD)
      if (symbol === 'MSFT') return send(200, CHART_MSFT)
      if (symbol === 'AAPL') return send(200, CHART_AAPL)
      // 其余代码回通用报价，支撑批量截断类用例
      return send(200, {
        chart: {
          result: [{ meta: { currency: 'USD', symbol, exchangeName: 'NMS', instrumentType: 'EQUITY', regularMarketPrice: 10, chartPreviousClose: 9.5, longName: `${symbol} Inc.` } }],
          error: null,
        },
      })
    }

    if (url.pathname.startsWith('/v1/finance/search')) {
      mockHits.search++
      lastSearch = {
        q: url.searchParams.get('q'),
        quotesCount: url.searchParams.get('quotesCount'),
        newsCount: url.searchParams.get('newsCount'),
        lang: url.searchParams.get('lang'),
        region: url.searchParams.get('region'),
        ua,
      }

      const send = (code, obj) => {
        res.writeHead(code, { 'Content-Type': 'application/json' })
        res.end(JSON.stringify(obj))
      }

      if ((lastSearch.q ?? '').includes('HTTPFAIL')) return send(500, { error: 'upstream exploded' })
      // 搜索与新闻共用端点：按条数参数区分返回体（与真实接口一致）
      if (Number(lastSearch.newsCount) > 0) return send(200, SEARCH_NEWS)
      return send(200, SEARCH_QUOTES)
    }

    res.writeHead(404, { 'Content-Type': 'application/json' })
    res.end(JSON.stringify({ error: `no mock for ${url.pathname}` }))
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
  console.log(`后端启动中：${BASE}（实例配置 BaseUrl 指向桩服务）`)

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
    const INST = `yf_main_${TS}`
    const BADURL = `yf_badurl_${TS}`

    const goodConfig = { BaseUrl: `http://127.0.0.1:${MOCK_PORT}`, TimeoutSeconds: 20, MaxSymbols: 10, MaxNews: 10, MaxSearchResults: 10, Lang: 'zh-CN', Region: 'US' }

    // ---------- quote：成功路径与解析 ----------
    check('创建 yahoo_finance 示例实例', (await save(admin, { pluginKey: INST, templeteKey: 'yahoo_finance', title: '桩服务 雅虎财经', description: 'mock', classifyId: 0, config: JSON.stringify(goodConfig) })).status === 200)

    const q = await run(admin, INST, JSON.stringify({ Mode: 'quote', Symbols: 'AAPL,MSFT' }))
    check('@DYN-S79a 批量报价运行成功且逐代码返回', q.json?.success === true && q.data?.Mode === 'quote' && q.data?.Quotes?.length === 2, `${q.status} ${q.text.slice(0, 240)}`)
    const aapl = q.data?.Quotes?.[0]
    check('@DYN-S79b meta 字段解析（名称/币种/交易所/类型/价格/昨收）', aapl?.Name === 'Apple Inc.' && aapl?.Currency === 'USD' && aapl?.Exchange === 'NASDAQ' && aapl?.InstrumentType === 'EQUITY' && aapl?.Price === 229.5 && aapl?.PreviousClose === 227.52, JSON.stringify(aapl ?? {}))
    check('@DYN-S79c 涨跌额与涨跌幅计算', aapl?.ChangeAmount === 1.98 && aapl?.ChangePercent === 0.87, JSON.stringify({ c: aapl?.ChangeAmount, p: aapl?.ChangePercent }))
    check('@DYN-S79d 日内高低/成交量/52 周/行情时间解析', aapl?.DayHigh === 231.2 && aapl?.DayLow === 228.1 && aapl?.Volume === 51234567 && aapl?.FiftyTwoWeekHigh === 237.25 && aapl?.FiftyTwoWeekLow === 164.08 && aapl?.MarketTime === '2024-10-04T00:00:00Z', JSON.stringify({ h: aapl?.DayHigh, l: aapl?.DayLow, v: aapl?.Volume, t: aapl?.MarketTime }))
    const msft = q.data?.Quotes?.[1]
    check('@DYN-S79e 精简 meta 容错（缺省字段为空不报错）', msft?.Name === 'Microsoft Corporation' && msft?.ChangeAmount === 2.8 && msft?.ChangePercent === 0.67 && msft?.DayHigh == null && msft?.MarketTime == null, JSON.stringify(msft ?? {}))
    check('@DYN-S79f 浏览器 User-Agent 已携带（上游反爬要求）', /^Mozilla\/5\.0/.test(lastChart?.ua ?? ''), lastChart?.ua ?? '')

    const qh = await run(admin, INST, JSON.stringify({ Mode: 'quote', Symbols: 'AAPL', IncludeHistory: true, Range: '5d', Interval: '1d' }))
    const hist = qh.data?.Quotes?.[0]?.History ?? []
    check('@DYN-S79g 走势序列保留最近 30 点且空收盘剔除', qh.json?.success === true && hist.length === 30 && hist.every((p) => p.Time && p.Close != null), JSON.stringify({ n: hist.length, first: hist[0], last: hist.at(-1) }))
    check('@DYN-S79h 走势区间与粒度按参数下发', lastChart?.range === '5d' && lastChart?.interval === '1d', JSON.stringify({ r: lastChart?.range, i: lastChart?.interval }))
    const qn = await run(admin, INST, JSON.stringify({ Mode: 'quote', Symbols: 'AAPL' }))
    check('@DYN-S79i 未开启走势时不带 range/interval 且 History 为空', qn.data?.Quotes?.[0]?.History?.length === 0 && lastChart?.range === null && lastChart?.interval === null, JSON.stringify({ r: lastChart?.range, i: lastChart?.interval }))

    // ---------- quote：失败隔离与截断 ----------
    const qm = await run(admin, INST, JSON.stringify({ Mode: 'quote', Symbols: 'AAPL,DEAD,HTTPFAIL,MSFT' }))
    check('@DYN-S80a 无效代码与 HTTP 500 归入错误清单，好代码继续返回', qm.json?.success === true && qm.data?.Quotes?.length === 2 && qm.data?.Errors?.length === 2, JSON.stringify({ q: qm.data?.Quotes?.length, e: qm.data?.Errors }))
    check('@DYN-S80b chart.error 文案进入错误消息', (qm.data?.Errors?.[0]?.Message ?? '').includes('No data found, symbol may be delisted'), qm.data?.Errors?.[0]?.Message ?? '')
    check('@DYN-S80c 上游 500 带状态码进入错误消息', (qm.data?.Errors?.[1]?.Message ?? '').includes('HTTP 500'), qm.data?.Errors?.[1]?.Message ?? '')
    const qt = await run(admin, INST, JSON.stringify({ Mode: 'quote', Symbols: 'AAPL,AAPL,MSFT,GOOG,AMZN,TSLA,META,NVDA,AMD,INTC,ORCL,CRM,ADBE,NFLX,UBER' }))
    check('@DYN-S80d 超 MaxSymbols 截断且重复代码去重', qt.data?.Quotes?.length === 10 && qt.data?.Truncated === true && qt.data?.Quotes?.[0]?.Symbol === 'AAPL', JSON.stringify({ n: qt.data?.Quotes?.length, t: qt.data?.Truncated }))

    // ---------- news：成功路径与参数下发 ----------
    const n = await run(admin, INST, JSON.stringify({ Mode: 'news', Query: '苹果', NewsCount: 99 }))
    check('@DYN-S81a 新闻搜索成功且空标题条目被丢弃', n.json?.success === true && n.data?.Mode === 'news' && n.data?.News?.length === 2, JSON.stringify(n.data?.News ?? []))
    check('@DYN-S81b 新闻字段解析（标题/发布方/链接/相关代码）', n.data?.News?.[0]?.Title === '苹果发布新款 AI 手机' && n.data?.News?.[0]?.Publisher === '新浪科技' && n.data?.News?.[0]?.Link === 'https://finance.sina.com.cn/apple-ai' && n.data?.News?.[0]?.RelatedTickers?.length === 2, JSON.stringify(n.data?.News?.[0] ?? {}))
    check('@DYN-S81c 发布时间转 ISO 且缺省为空', n.data?.News?.[0]?.PublishTime === '2024-10-04T00:00:00Z' && n.data?.News?.[1]?.PublishTime == null, JSON.stringify({ a: n.data?.News?.[0]?.PublishTime, b: n.data?.News?.[1]?.PublishTime }))
    check('@DYN-S81d 条数按实例上限 clamp 且 quotesCount=0、实例 lang/region 透传', lastSearch?.newsCount === '10' && lastSearch?.quotesCount === '0' && lastSearch?.lang === 'zh-CN' && lastSearch?.region === 'US', JSON.stringify(lastSearch ?? {}))

    const n2 = await run(admin, INST, JSON.stringify({ Mode: 'news', Query: '苹果', Region: 'CN', Lang: 'en-US' }))
    check('@DYN-S81e 请求级 Region/Lang 覆盖实例默认', n2.json?.success === true && lastSearch?.region === 'CN' && lastSearch?.lang === 'en-US', JSON.stringify({ r: lastSearch?.region, l: lastSearch?.lang }))

    // ---------- search：代码解析 ----------
    const s = await run(admin, INST, JSON.stringify({ Mode: 'search', Query: 'apple', SearchCount: 5 }))
    check('@DYN-S82a 代码候选解析（shortname 优先、longname 回落、exchDisp/typeDisp）', s.json?.success === true && s.data?.Mode === 'search' && s.data?.Candidates?.length === 2 && s.data?.Candidates?.[0]?.Name === 'Apple Inc.' && s.data?.Candidates?.[1]?.Name === 'Apple Inc. (Sao Paulo)' && s.data?.Candidates?.[1]?.Exchange === 'Sao Paulo' && s.data?.Candidates?.[0]?.TypeDisplay === 'Equity', JSON.stringify(s.data?.Candidates ?? []))
    check('@DYN-S82b 候选条数下发且 newsCount=0', lastSearch?.quotesCount === '5' && lastSearch?.newsCount === '0', JSON.stringify({ qc: lastSearch?.quotesCount, nc: lastSearch?.newsCount }))

    // ---------- 参数与配置校验 ----------
    const badMode = await run(admin, INST, JSON.stringify({ Mode: 'noSuchMode', Symbols: 'AAPL' }))
    check('@DYN-S83a 非法 Mode 400', badMode.json?.success === false && /Mode/.test(badMode.json?.error ?? ''), badMode.json?.error ?? '')
    const noSym = await run(admin, INST, JSON.stringify({ Mode: 'quote', Symbols: '  ' }))
    check('@DYN-S83b quote 缺 Symbols 400', noSym.json?.success === false && /Symbols/.test(noSym.json?.error ?? ''), noSym.json?.error ?? '')
    const noQuery = await run(admin, INST, JSON.stringify({ Mode: 'news', Query: ' ' }))
    check('@DYN-S83c news 缺 Query 400', noQuery.json?.success === false && /Query/.test(noQuery.json?.error ?? ''), noQuery.json?.error ?? '')
    const badRange = await run(admin, INST, JSON.stringify({ Mode: 'quote', Symbols: 'AAPL', IncludeHistory: true, Range: '3h' }))
    check('@DYN-S83d 非法 Range 400', badRange.json?.success === false && /Range/.test(badRange.json?.error ?? ''), badRange.json?.error ?? '')

    check('创建非法 BaseUrl 实例', (await save(admin, { pluginKey: BADURL, templeteKey: 'yahoo_finance', title: '桩服务 非法地址', description: 'mock', classifyId: 0, config: JSON.stringify({ BaseUrl: 'ftp://not-a-url' }) })).status === 200)
    const badRun = await run(admin, BADURL, JSON.stringify({ Mode: 'quote', Symbols: 'AAPL' }))
    check('@DYN-S83e 非法 BaseUrl 被 InitAsync 拦截', badRun.json?.success === false && /BaseUrl/.test(badRun.json?.error ?? ''), badRun.json?.error ?? '')

    // ---------- news 模式上游错误归一 ----------
    const nFail = await run(admin, INST, JSON.stringify({ Mode: 'news', Query: 'HTTPFAIL-keyword' }))
    check('@DYN-S84a news 模式上游 500 归一为可读失败', nFail.json?.success === false && /HTTP 500/.test(nFail.json?.error ?? '') && /upstream exploded/.test(nFail.json?.error ?? ''), nFail.json?.error ?? '')

    check('桩服务确被命中（chart + search）', mockHits.chart >= 18 && mockHits.search >= 4, JSON.stringify(mockHits))

    // ---------- 清理 ----------
    await del(admin, INST)
    await del(admin, BADURL)

    console.log(`\n=== 雅虎财经插件桩服务 E2E: PASS ${PASS} / FAIL ${FAIL} ===`)
    if (FAIL > 0) process.exitCode = 1
  } finally {
    if (process.env.YF_KEEP_BACKEND === '1') console.log(`YF_KEEP_BACKEND=1，保留后端 pid=${backend.pid}`)
    else await killBackend(backend)
    mock.close()
  }
}

main().catch((e) => { console.error('E2E 异常:', e.message); process.exitCode = 1 })
