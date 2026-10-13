// 新闻热榜插件「桩服务」端到端（场景 @DYN-S106 ~ @DYN-S116）
// 做法：本地起一个 16 平台热榜数据桩（按官方报文结构回放）→ 用 .builds/hotboard 独立输出起一个后端（绕开运行中后端的 DLL 锁）→
//       创建 news_hot_board 实例（Gateway 指向桩服务）并运行 → 断言「动态插件实例 → 注册表模板 → 渠道并发拉取 →
//       网关改写 → 各平台响应解析 → 合并/截断/失败隔离」整条链路。无需真实外网、不受平台风控影响。
// 前置：dotnet build src/MoAI/MoAI.csproj -o .builds/hotboard；postgres / redis / rabbitmq 已就绪。
// 用法：node local-dev/hot-board-e2e.mjs
// 环境变量：HB_BACKEND_PORT（默认 5281）｜HB_MOCK_PORT（默认 5280）｜HB_KEEP_BACKEND=1 保留后端供排查
import { createServer } from 'node:http'
import { spawn } from 'node:child_process'
import crypto from 'node:crypto'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const REPO_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const BACKEND_PORT = Number(process.env.HB_BACKEND_PORT ?? 5281)
const MOCK_PORT = Number(process.env.HB_MOCK_PORT ?? 5280)
const BASE = `http://127.0.0.1:${BACKEND_PORT}`

let PASS = 0, FAIL = 0
const check = (name, cond, detail = '') => {
  if (cond) { PASS++; console.log(`PASS | ${name}`) }
  else { FAIL++; console.log(`FAIL | ${name} ${detail ? '— ' + detail : ''}`) }
}

// ---------------- 桩服务：按各平台官方报文结构回放 ----------------
// 每平台 3 条有效条目（qq 额外带 1 条 TIP 横幅），标题平台名可辨识
const hits = {}          // 路由命中计数
const lastUa = {}        // 各路由最近一次请求的 User-Agent
let lastDouyinCookie = null
let failHupu = false

const hit = (key, req) => {
  hits[key] = (hits[key] ?? 0) + 1
  lastUa[key] = req.headers['user-agent'] ?? ''
}

const sendJson = (res, obj) => {
  res.writeHead(200, { 'Content-Type': 'application/json' })
  res.end(JSON.stringify(obj))
}

const three = (title) => [1, 2, 3].map((n) => `${title}${n}`)

const MOCKS = {
  // 知乎：热度 detail_text 支持「万」换算
  'api.zhihu.com/topstory/hot-lists/total': (req, res) => {
    hit('zhihu', req)
    sendJson(res, {
      data: three('知乎热榜话题').map((t, i) => ({
        target: { title: t, excerpt: `${t}摘要`, url: `https://api.zhihu.com/questions/67890${i}`, id: 7173456789012300000 + i },
        detail_text: i === 0 ? '4589 万热度' : `${1000 + i} 热度`,
        children: i === 0 ? [{ thumbnail: 'https://pic1.zhimg.com/t.jpg' }] : [],
      })),
    })
  },
  // 微博：无热度值，搜索链接由词条拼出
  'weibo.com/ajax/side/hotSearch': (req, res) => {
    hit('weibo', req)
    sendJson(res, {
      data: {
        realtime: three('微博热搜词条').map((w, i) => ({ word: w, word_scheme: `#${w}#`, mid: `512345${i}` })),
      },
    })
  },
  // 36氪：POST 接口，itemId 为 19 位长整型数字 token —— 手工拼 JSON 报文保留字面量（JS number 会丢精度）
  'gateway.36kr.com/api/mis/nav/home/nav/rank/hot': (req, res) => {
    hit('36kr', req)
    const rows = three('36氪热文').map((t, i) => `{"itemId":717123456789012345${i},"templateMaterial":{"widgetTitle":"${t}","widgetImage":"https://img.36kr.com/${i}.png","statCollect":"${100 + i}"}}`)
    res.writeHead(200, { 'Content-Type': 'application/json' })
    res.end(`{"data":{"hotRankList":[${rows.join(',')}]}}`)
  },
  // 稀土掘金：content.content_id + content_counter.view
  'api.juejin.cn/content_api/v1/content/article_rank': (req, res) => {
    hit('juejin', req)
    sendJson(res, {
      err_no: 0,
      data: three('掘金热文').map((t, i) => ({
        content: { content_id: `769483519210786000${i}`, title: t, brief: `${t}摘要` },
        content_counter: { view: 3686 - i, like: 46 },
      })),
    })
  },
  // 今日头条：ClusterIdStr + HotValue + Image.url
  'www.toutiao.com/hot-event/hot-board/': (req, res) => {
    hit('toutiao', req)
    sendJson(res, {
      data: three('头条热点').map((t, i) => ({
        Title: t, ClusterIdStr: `730123456789012300${i}`, HotValue: `${987654 - i}`,
        Image: i === 0 ? { url: 'https://p3-sign.toutiaoimg.com/x~tplv-a.jpg' } : undefined,
      })),
    })
  },
  // 百度：HTML 注释内嵌 s-data JSON，首卡含嵌套子榜
  'top.baidu.com/board': (req, res) => {
    hit('baidu', req)
    const sData = JSON.stringify({
      data: {
        cards: [{
          content: [
            { word: '百度热搜词1', desc: '词条描述', query: '百度热搜词1', hotScore: '4965021', img: 'https://b.bdstatic.com/a.png' },
            { content: [{ word: '百度子榜词', query: '百度子榜词', hotTag: '热' }] },
            { word: '百度热搜词2', query: '百度热搜词2', hotScore: '4965022' },
          ],
        }],
      },
    })
    res.writeHead(200, { 'Content-Type': 'text/html' })
    res.end(`<!DOCTYPE html><html><body><script><!--s-data:${sData}--></script></body></html>`)
  },
  // B站：nav 取 WBI key（桩 key 各 32 字符防混淆表越界）→ ranking/v2 带 w_rid
  'api.bilibili.com/x/web-interface/nav': (req, res) => {
    hit('bilibili-nav', req)
    sendJson(res, {
      wbi_img: {
        img_url: 'https://i0.hdslb.com/bfs/wbi/aaaabbbbccccddddeeeeffffgggghhhh.png',
        sub_url: 'https://i0.hdslb.com/bfs/wbi/11112222333344445555666677778888.png',
      },
    })
  },
  'api.bilibili.com/x/web-interface/ranking/v2': (req, res) => {
    hit('bilibili', req)
    sendJson(res, {
      data: {
        list: three('B站热门视频').map((t, i) => ({
          bvid: `BV1xx411c7m${['A', 'B', 'C'][i]}`,
          title: t,
          desc: `${t}简介`,
          pic: `http://i0.hdslb.com/bfs/${i}.jpg`,
          stat: { view: 523456 - i },
        })),
      },
    })
  },
  // 抖音：先发 Set-Cookie，热榜请求应回带 Cookie
  'www.douyin.com/passport/general/login_guiding_strategy/': (req, res) => {
    hit('douyin-csrf', req)
    res.writeHead(200, {
      'Content-Type': 'application/json',
      'Set-Cookie': 'passport_csrf_token=hb_e2e_token; path=/; domain=douyin.com',
    })
    res.end('{}')
  },
  'www.douyin.com/aweme/v1/web/hot/search/list/': (req, res) => {
    hit('douyin', req)
    lastDouyinCookie = req.headers.cookie ?? null
    sendJson(res, {
      data: {
        word_list: three('抖音热点词').map((w, i) => ({ word: w, sentence_id: `731123456789012300${i}`, hot_value: `${523456 - i}` })),
      },
    })
  },
  // IT之家：HTML 榜单页
  'm.ithome.com/rankm/': (req, res) => {
    hit('ithome', req)
    const rows = three('IT之家热文').map((t, i) => `
      <div class="placeholder">
        <a class="plc-title" href="https://m.ithome.com/html/70145${i}.htm">${t}</a>
        <span class="review-num">${66 + i}</span>
        <img data-original="https://img.ithome.com/${i}.png">
      </div>`).join('')
    res.writeHead(200, { 'Content-Type': 'text/html' })
    res.end(`<!DOCTYPE html><html><body><div class="rank-box">${rows}</div></body></html>`)
  },
  // 虎扑：可通过控制端点注入 500
  'm.hupu.com/api/v2/bbs/topicThreads': (req, res) => {
    hit('hupu', req)
    if (failHupu) {
      res.writeHead(500, { 'Content-Type': 'application/json' })
      res.end('{"error":"hupu exploded"}')
      return
    }
    sendJson(res, {
      data: {
        topicThreads: three('步行街热帖').map((t, i) => ({ title: t, tid: 3141592 + i, replies: 876 - i })),
      },
    })
  },
  // 虎嗅：br 分段快讯
  'moment-api.huxiu.com/web-v3/moment/feed': (req, res) => {
    hit('huxiu', req)
    sendJson(res, {
      data: {
        moment_list: {
          datalist: three('虎嗅快讯').map((t, i) => ({
            content: `${t}首句。<br>${t}第二段摘要。`,
            object_id: 98765432100 + i,
            count_info: { agree_num: `${456 - i}` },
          })),
        },
      },
    })
  },
  // 网易：url 优先、docid 回退
  'm.163.com/fe/api/hot/news/flow': (req, res) => {
    hit('netease-news', req)
    sendJson(res, {
      data: {
        list: three('网易热点').map((t, i) => ({
          title: t,
          docid: `ABC123${i}`,
          source: `媒体${i}`,
          imgsrc: `https://nimg.ws.126.net/${i}.png`,
        })),
      },
    })
  },
  // 腾讯：首条 TIP 横幅应被跳过
  'r.inews.qq.com/gw/event/hot_ranking_list': (req, res) => {
    hit('qq-news', req)
    sendJson(res, {
      idlist: [{
        newslist: [
          { id: 'TIP20250101', title: '横幅提示语' },
          ...three('腾讯热点').map((t, i) => ({
            id: `2025010${i}A0ABCDE`,
            title: t,
            abstract: `${t}摘要`,
            miniProShareImage: `https://inews.gtimg.com/${i}.jpg`,
            hotEvent: { hotScore: `${2637459 - i}` },
          })),
        ],
      }],
    })
  },
  // 新浪：JS 变量壳 + 千分位热度
  'top.news.sina.com.cn/ws/GetTopDataList.php': (req, res) => {
    hit('sina-news', req)
    const rows = three('新浪头条').map((t, i) => ({
      title: t, id: `${1001 + i}`, url: `https://finance.sina.com.cn/${i}`, media: `新浪科技${i}`, top_num: i === 0 ? '4,567' : `${89 + i}`,
    }))
    res.writeHead(200, { 'Content-Type': 'text/plain' })
    res.end(`var data = ${JSON.stringify({ data: rows })};`)
  },
  // 澎湃：contId 拼 newsDetail 地址
  'cache.thepaper.cn/contentapi/wwwIndex/rightSidebar': (req, res) => {
    hit('thepaper', req)
    sendJson(res, {
      data: {
        hotNews: three('澎湃热点').map((t, i) => ({
          name: t, contId: `789012${i}`, pic: `https://img.thepaper.cn/${i}.jpg`, praiseTimes: `${1234 - i}`,
        })),
      },
    })
  },
  // 极客公园：homepage_posts[].post
  'mainssl.geekpark.net/api/v2': (req, res) => {
    hit('geekpark', req)
    sendJson(res, {
      homepage_posts: three('极客公园热文').map((t, i) => ({
        post: { id: 5123456 + i, title: t, abstract: `${t}摘要`, cover_url: `https://images.geekpark.net/${i}.png`, views: `${12345 - i}` },
      })),
    })
  },
}

function startMock() {
  const server = createServer((req, res) => {
    const url = new URL(req.url, 'http://127.0.0.1')
    if (url.pathname === '/__control/fail_hupu') {
      failHupu = req.method === 'POST'
      sendJson(res, { failHupu })
      return
    }

    // 网关改写约定：{Gateway}/https/{域名}/{路径}
    const route = url.pathname.replace(/^\/https\//, '')
    const mock = MOCKS[route]
    if (mock) return mock(req, res)

    res.writeHead(404, { 'Content-Type': 'application/json' })
    res.end(JSON.stringify({ error: `no mock for ${url.pathname}` }))
  })
  return new Promise((resolve) => server.listen(MOCK_PORT, '127.0.0.1', () => resolve(server)))
}

// ---------------- 后端：独立输出目录 DLL（绕开运行中后端的 DLL 锁） ----------------
function startBackend() {
  const dll = path.join(REPO_ROOT, '.builds', 'hotboard', 'MoAI.dll')
  if (!fs.existsSync(dll)) {
    throw new Error(`未找到 ${dll}，请先执行：dotnet build src/MoAI/MoAI.csproj -o .builds/hotboard`)
  }
  const child = spawn('dotnet', [dll], {
    cwd: path.join(REPO_ROOT, 'src', 'MoAI'),
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
  for (let i = 0; i < 90; i++) {
    if (child.exitCode !== null) throw new Error(`后端进程提前退出（code=${child.exitCode}）：\n${child.getLog().slice(-2000)}`)
    try {
      const r = await fetch(`${BASE}/api/common/serverinfo`, { signal: AbortSignal.timeout(3000) })
      if (r.ok) return true
    } catch { /* 未就绪 */ }
    await sleep(2000)
  }
  throw new Error(`后端 ${BASE} 未在 180s 内就绪：\n${child.getLog().slice(-2000)}`)
}

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms))

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

const ALL_SOURCES = ['36kr', 'zhihu', 'weibo', 'juejin', 'toutiao', 'baidu', 'bilibili', 'douyin', 'ithome', 'hupu', 'huxiu', 'thepaper', 'geekpark', 'qq-news', 'netease-news', 'sina-news']
const EXPECT_PER_SOURCE = 3

function assertRankedBySource(data) {
  const bySource = new Map()
  for (const item of data.NewsList) {
    if (!bySource.has(item.Source)) bySource.set(item.Source, [])
    bySource.get(item.Source).push(item)
  }
  for (const [source, items] of bySource) {
    const ranks = items.map((x) => x.Rank)
    const expected = Array.from({ length: items.length }, (_, i) => i + 1)
    if (JSON.stringify(ranks) !== JSON.stringify(expected)) return `来源 ${source} 排名异常：${ranks.join(',')}`
  }
  return null
}

async function main() {
  const mock = await startMock()
  console.log(`桩服务已就绪：http://127.0.0.1:${MOCK_PORT}`)
  const backend = startBackend()
  console.log(`后端启动中：${BASE}（实例配置 Gateway 指向桩服务）`)

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
    const INST = `hb_main_${TS}`
    const INST_LIMIT = `hb_limit_${TS}`
    const INST_DEAD = `hb_dead_${TS}`
    const INST_BADGW = `hb_badgw_${TS}`

    // ---------- 实例管理 ----------
    const goodConfig = { Limit: 20, Gateway: `http://127.0.0.1:${MOCK_PORT}` }
    const saved = await save(admin, {
      pluginKey: INST, templeteKey: 'news_hot_board', title: '桩服务 新闻热榜', description: 'mock', classifyId: 0,
      config: JSON.stringify(goodConfig),
    })
    check('@DYN-S106a 创建 news_hot_board 实例', saved.status === 200, `${saved.status} ${saved.text.slice(0, 200)}`)
    // 同 key 重复提交按更新语义处理（与 dynamic-plugin-e2e DYN-S5 一致），配置整体覆盖
    const dup = await save(admin, {
      pluginKey: INST, templeteKey: 'news_hot_board', title: '桩服务 新闻热榜 v2', description: 'mock', classifyId: 0,
      config: JSON.stringify(goodConfig),
    })
    check('@DYN-S106b 同 key 重复提交走更新（200）', dup.status === 200, `${dup.status} ${dup.text.slice(0, 160)}`)

    // ---------- 参数校验 ----------
    const emptyRun = await run(admin, INST, JSON.stringify({ Sources: [] }))
    check('@DYN-S107a 空来源 400 并提示可选值', emptyRun.json?.success === false && /Sources/.test(emptyRun.json?.error ?? '') && /36kr/.test(emptyRun.json?.error ?? ''), emptyRun.json?.error ?? '')
    const unknownRun = await run(admin, INST, JSON.stringify({ Sources: ['zhihu', '抖音号'] }))
    check('@DYN-S107b 未知来源 400 并原样回显', unknownRun.json?.success === false && /未知热榜来源/.test(unknownRun.json?.error ?? '') && /抖音号/.test(unknownRun.json?.error ?? ''), unknownRun.json?.error ?? '')

    // ---------- 全渠道成功路径 ----------
    const all = await run(admin, INST, JSON.stringify({ Sources: [...ALL_SOURCES, '知乎', 'b站'] }))
    check('@DYN-S108a 16 渠道全部成功且中文别名归一', all.json?.success === true && all.data?.Errors?.length === 0, `${all.status} ${all.text.slice(0, 300)}`)
    const sourcesInData = [...new Set((all.data?.NewsList ?? []).map((x) => x.Source))]
    check('@DYN-S108b 返回覆盖全部 16 来源（别名去重）', sourcesInData.length === 16 && ALL_SOURCES.every((s) => sourcesInData.includes(s)), sourcesInData.join(','))
    check('@DYN-S108c 每来源 3 条共 48 条', all.data?.NewsList?.length === 16 * EXPECT_PER_SOURCE, `实际 ${all.data?.NewsList?.length}`)
    check('@DYN-S108d 来源内排名连续 1..n', assertRankedBySource(all.data ?? {}) === null, assertRankedBySource(all.data ?? {}) ?? '')
    const sample = all.data?.NewsList?.find((x) => x.Source === 'zhihu')
    check('@DYN-S108e 条目结构含 Source/SourceName/Rank/Title/Url', sample?.SourceName === '知乎' && typeof sample?.Title === 'string' && sample?.Url?.length > 0, JSON.stringify(sample ?? {}))

    // ---------- 关键字段解析抽查 ----------
    const pick = (source, rank) => (all.data?.NewsList ?? []).find((x) => x.Source === source && x.Rank === rank)
    check('@DYN-S109a 知乎「万」热度换算 + 问题链接拼回', pick('zhihu', 1)?.HotValue === 45890000 && pick('zhihu', 1)?.Url === 'https://www.zhihu.com/question/678900' && pick('zhihu', 1)?.Cover === 'https://pic1.zhimg.com/t.jpg', JSON.stringify(pick('zhihu', 1) ?? {}))
    check('@DYN-S109b 36氪长整型 itemId 精度不丢失', pick('36kr', 1)?.Url === 'https://www.36kr.com/p/7171234567890123450', pick('36kr', 1)?.Url ?? '')
    check('@DYN-S109c 掘金文章链接与阅读数', pick('juejin', 1)?.Url === 'https://juejin.cn/post/7694835192107860000' && pick('juejin', 1)?.HotValue === 3686, JSON.stringify(pick('juejin', 1) ?? {}))
    const wbUrl = pick('weibo', 1)?.Url ?? ''
    check('@DYN-S109d 微博搜索链接转义', wbUrl.startsWith('https://s.weibo.com/weibo?q=') && decodeURIComponent(wbUrl).includes('q=微博热搜词条1'), wbUrl)
    check('@DYN-S109e 新浪千分位热度归一', pick('sina-news', 1)?.HotValue === 4567 && (pick('sina-news', 1)?.Desc ?? '').startsWith('新浪科技'), JSON.stringify(pick('sina-news', 1) ?? {}))
    check('@DYN-S109f 腾讯 TIP 横幅被跳过（仍 3 条）', (all.data?.NewsList ?? []).filter((x) => x.Source === 'qq-news').length === 3 && (all.data?.NewsList ?? []).every((x) => !/^TIP/.test(x.Url)), '')
    check('@DYN-S110a 百度嵌套子榜下钻 + 搜索链接', (all.data?.NewsList ?? []).some((x) => x.Source === 'baidu' && x.Title === '百度子榜词') && (pick('baidu', 1)?.Url ?? '').startsWith('https://www.baidu.com/s?wd='), '')
    check('@DYN-S110b IT之家短链改写 www 站文章地址', pick('ithome', 1)?.Url === 'https://www.ithome.com/0/701/450.htm', pick('ithome', 1)?.Url ?? '')
    check('@DYN-S110c 虎嗅 br 分段拆标题摘要', pick('huxiu', 1)?.Title === '虎嗅快讯1首句' && pick('huxiu', 1)?.Desc === '虎嗅快讯1第二段摘要。', JSON.stringify(pick('huxiu', 1) ?? {}))
    check('@DYN-S110d B站 http 封面升级 https + 播放量热度', pick('bilibili', 1)?.Cover === 'https://i0.hdslb.com/bfs/0.jpg' && pick('bilibili', 1)?.HotValue === 523456, JSON.stringify(pick('bilibili', 1) ?? {}))

    // ---------- 网关改写与浏览器指纹 ----------
    check('@DYN-S111a 16 个上游域名全部经网关命中桩服务', ALL_SOURCES.every((s) => (hits[s === 'bilibili' ? 'bilibili' : s] ?? 0) >= 1), JSON.stringify(hits))
    check('@DYN-S111b 请求携带浏览器 User-Agent（反爬要求）', /^Mozilla\/5\.0/.test(lastUa.zhihu ?? ''), lastUa.zhihu ?? '')
    check('@DYN-S111c 抖音 CSRF Cookie 预取并回带', (lastDouyinCookie ?? '').includes('passport_csrf_token=hb_e2e_token'), lastDouyinCookie ?? 'null')

    // ---------- Limit 截断 ----------
    const savedLimit = await save(admin, {
      pluginKey: INST_LIMIT, templeteKey: 'news_hot_board', title: '桩服务 截断', description: 'mock', classifyId: 0,
      config: JSON.stringify({ Limit: 2, Gateway: `http://127.0.0.1:${MOCK_PORT}` }),
    })
    check('@DYN-S112a 创建 Limit=2 实例', savedLimit.status === 200, `${savedLimit.status}`)
    const limited = await run(admin, INST_LIMIT, JSON.stringify({ Sources: ['zhihu', 'weibo'] }))
    check('@DYN-S112b 每来源按 Limit 截断到 2 条', limited.json?.success === true && limited.data?.NewsList?.length === 4
      && limited.data?.NewsList?.filter((x) => x.Source === 'zhihu').every((x) => x.Rank <= 2), `实际 ${limited.data?.NewsList?.length}`)

    // ---------- 失败隔离 ----------
    await fetch(`http://127.0.0.1:${MOCK_PORT}/__control/fail_hupu`, { method: 'POST' })
    const partial = await run(admin, INST, JSON.stringify({ Sources: ['zhihu', 'hupu'] }))
    check('@DYN-S113a 单渠道 500 不拖垮整轮（虎扑进 Errors，知乎照常）', partial.json?.success === true
      && partial.data?.NewsList?.length === 3
      && partial.data?.NewsList?.every((x) => x.Source === 'zhihu')
      && partial.data?.Errors?.length === 1
      && /虎扑/.test(partial.data?.Errors?.[0] ?? '')
      && /HTTP 500/.test(partial.data?.Errors?.[0] ?? ''), JSON.stringify(partial.data?.Errors ?? []))
    await fetch(`http://127.0.0.1:${MOCK_PORT}/__control/fail_hupu`, { method: 'DELETE' })

    // ---------- 全部失败归一 ----------
    const savedDead = await save(admin, {
      pluginKey: INST_DEAD, templeteKey: 'news_hot_board', title: '桩服务 全挂', description: 'mock', classifyId: 0,
      config: JSON.stringify({ Gateway: 'http://127.0.0.1:9' }),
    })
    const deadRun = await run(admin, INST_DEAD, JSON.stringify({ Sources: ['zhihu'] }))
    check('@DYN-S114a 全来源失败归一为可读错误（502）', savedDead.status === 200 && deadRun.json?.success === false && /全部来源抓取失败/.test(deadRun.json?.error ?? '') && /知乎/.test(deadRun.json?.error ?? ''), deadRun.json?.error ?? '')

    // ---------- 非法网关被 InitAsync 拦截 ----------
    await save(admin, {
      pluginKey: INST_BADGW, templeteKey: 'news_hot_board', title: '桩服务 非法网关', description: 'mock', classifyId: 0,
      config: JSON.stringify({ Gateway: 'ftp://not-a-gateway' }),
    })
    const badGwRun = await run(admin, INST_BADGW, JSON.stringify({ Sources: ['zhihu'] }))
    check('@DYN-S115a 非法 Gateway 被 InitAsync 拦截', badGwRun.json?.success === false && /Gateway/.test(badGwRun.json?.error ?? ''), badGwRun.json?.error ?? '')

    // ---------- 删除后不可运行 ----------
    const removed = await del(admin, INST)
    const afterDel = await run(admin, INST, JSON.stringify({ Sources: ['zhihu'] }))
    check('@DYN-S116a 删除实例后运行 404', removed.status === 200 && afterDel.status === 404, `del=${removed.status} run=${afterDel.status}`)

    // ---------- 清理 ----------
    for (const key of [INST_LIMIT, INST_DEAD, INST_BADGW]) {
      await del(admin, key)
    }
    check('桩服务确被命中（知乎/掘金/腾讯）', (hits.zhihu ?? 0) >= 2 && (hits.juejin ?? 0) >= 1 && (hits['qq-news'] ?? 0) >= 1, JSON.stringify(hits))
  } finally {
    if (process.env.HB_KEEP_BACKEND === '1') {
      console.log(`HB_KEEP_BACKEND=1，保留后端 ${BASE} 供排查`)
    } else {
      await killBackend(backend)
    }
    mock.close()
  }

  console.log(`\n通过 ${PASS}，失败 ${FAIL}`)
  if (FAIL > 0) process.exit(1)
}

main().catch((err) => {
  console.error(err)
  process.exit(1)
})
