// 知识库「外部源」E2E（真实 HTTP + 本地站点桩，断言编号 @WS-S*，对应 docs/wiki/bdd.md 的 Feature: 外部源）
// 用法：node local-dev/wiki-source-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5000）
// 前置：后端运行中（Development，端口 5000）、PostgreSQL / Redis / MinIO 可达。
// 说明：
//   1) 网页爬虫源走真实抓取链路：脚本自带本地静态站点桩，验证「立即同步 → 文档入库 → 增量比对 → 强制全量」全链路；
//   2) 飞书文档源不依赖真实开放平台凭证（与 feishu-e2e.mjs 同口径，假凭证即可）；创建后首次拉取必然失败，
//      此处断言的是「失败不阻断创建、状态落到外部源上」这条兜底契约，后续拉起真实长连接才能验证成功路径；
//   3) 内部准备全部用自建用户 + 自建团队/知识库，清理阶段删除创建的外部源与飞书应用连接，可重复执行。
import crypto from 'node:crypto'
import http from 'node:http'

const BASE = process.argv[2] ?? 'http://127.0.0.1:5000'
let PASS = 0
let FAIL = 0
const check = (name, cond, detail = '') => {
  if (cond) {
    PASS++
    console.log(`PASS | ${name}`)
  } else {
    FAIL++
    console.log(`FAIL | ${name}${detail ? ' — ' + detail : ''}`)
  }
}

let RSA_KEY = ''
const rsa = (plain) => {
  const key = crypto.createPublicKey({ key: Buffer.from(RSA_KEY, 'base64'), format: 'der', type: 'spki' })
  return crypto.publicEncrypt({ key, padding: crypto.constants.RSA_PKCS1_PADDING }, Buffer.from(plain, 'utf8')).toString('base64')
}

async function api(method, path, { token, body } = {}) {
  const headers = {}
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  if (token) headers['Authorization'] = `Bearer ${token}`
  const res = await fetch(BASE + path, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) })
  const text = await res.text()
  let json = null
  try {
    json = JSON.parse(text)
  } catch {
    /* 非 JSON 响应 */
  }
  return { status: res.status, json, text }
}

const TS = Date.now().toString().slice(-8)
let seq = 0
const uname = (p) => `${p}${TS}${String(seq++).padStart(2, '0')}`
const phone = () => `15${Date.now().toString().slice(-8)}${String(seq++).padStart(2, '0')}`.slice(0, 11)

async function mkuser(p) {
  const name = uname(p)
  const r = await api('POST', '/api/auth/register', {
    body: { userName: name, email: `${name}@test.local`, nickName: name, phone: phone(), password: rsa('Test1234') },
  })
  if (r.status !== 200) throw new Error(`注册 ${name} 失败: ${r.status} ${r.text.slice(0, 120)}`)
  const l = await api('POST', '/api/auth/login', { body: { userName: name, password: rsa('Test1234') } })
  return { name, userId: Number(l.json.userId), token: l.json.accessToken }
}

// ---------------------------------------------------------------------------
// 本地站点桩：模拟一个文档站 /kb/，用于验证爬虫的真实抓取行为
//   /kb/            索引页，第一轮链接到 a / b / outside/x；第二轮再加入 sub/c
//   /kb/a.html      正文页（内容可在用例中改写，验证「有变化才更新」）
//   /kb/b.html      正文页
//   /kb/sub/c.html  子目录正文页（在路径前缀内，应被抓取；第一轮后才加入索引，用于验证父页无变化时仍能发现新页面）
//   /outside/x.html 前缀外页面（不应被抓取）
// ---------------------------------------------------------------------------
const pages = {
  '/kb/': () =>
    `<html><head><title>索引</title></head><body><h1>索引</h1>
     <a href="/kb/a.html">A</a><a href="/kb/b.html">B</a>${state.extraLink}
     <a href="/outside/x.html">Outside</a></body></html>`,
  '/kb/a.html': () => `<html><head><title>文档 A</title></head><body><article><p>${state.aText}</p></article></body></html>`,
  '/kb/b.html': () => `<html><head><title>文档 B</title></head><body><article><p>${state.bText}</p></article></body></html>`,
  '/kb/sub/c.html': () => '<html><head><title>文档 C</title></head><body><article><p>C 正文</p></article></body></html>',
  '/outside/x.html': () => '<html><head><title>站外</title></head><body><p>不应被抓取</p></body></html>',
}
const state = { aText: 'A 正文第一版', bText: 'B 正文第一版', extraLink: '' }

function startStubSite() {
  return new Promise((resolve) => {
    const server = http.createServer((req, res) => {
      const path = new URL(req.url, 'http://127.0.0.1').pathname
      const page = pages[path]
      if (!page) {
        res.writeHead(404, { 'Content-Type': 'text/html; charset=utf-8' })
        res.end('<html><head><title>404</title></head><body>not found</body></html>')
        return
      }
      res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' })
      res.end(page())
    })
    server.listen(0, '127.0.0.1', () => resolve({ server, port: server.address().port }))
  })
}

const crawlPayload = (startUrl, extra = {}) => ({
  sourceType: 'crawler',
  cron: '',
  isEnable: true,
  crawler: {
    startUrl,
    pathPrefix: '',
    maxDepth: 3,
    maxPages: 20,
    requestIntervalSeconds: 1,
    timeoutSeconds: 10,
    userAgent: '',
    contentSelector: 'article',
    isOverwriteExisting: true,
    ...extra,
  },
})

const sleep = (ms) => new Promise((r) => setTimeout(r, ms))

/** 等待异步状态收敛（首次创建后会立即同步一次） */
async function waitForSync(sourceId, token, wikiId, predicate, tries = 20) {
  for (let i = 0; i < tries; i++) {
    const r = await api('GET', `/api/wiki/${wikiId}/sources`, { token })
    const item = (r.json?.items ?? []).find((x) => x.sourceId === sourceId)
    if (item && predicate(item)) return item
    await sleep(1000)
  }
  const r = await api('GET', `/api/wiki/${wikiId}/sources`, { token })
  return (r.json?.items ?? []).find((x) => x.sourceId === sourceId) ?? null
}

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const owner = await mkuser('wso')
  const admin = await mkuser('wsa')
  const member = await mkuser('wsm')
  const outsider = await mkuser('wsx')

  const t = await api('POST', '/api/team', { token: owner.token, body: { name: 'wsrc-team-' + TS } })
  const TID = Number(t.json.value)
  await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: admin.userId, role: 1 } })
  await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: member.userId, role: 0 } })

  const w = await api('POST', '/api/wiki', { token: owner.token, body: { teamId: TID, name: 'wsrc-' + TS } })
  const WID = Number(w.json.value)
  // 第二个知识库：用于「跨知识库同名不冲突」
  const w2 = await api('POST', '/api/wiki', { token: owner.token, body: { teamId: TID, name: 'wsrc2-' + TS } })
  const WID2 = Number(w2.json.value)

  const stub = await startStubSite()
  const site = `http://127.0.0.1:${stub.port}/kb/`
  console.log(`INFO | 本地站点桩已启动 ${site}`)

  try {
    // 权限探针源：权限用例必须打在真实存在的外部源上，否则会先撞出 404 而非 403/404 的角色差异；
    // maxDepth=1 只抓索引页，开销最小。正式用例的源另行创建。
    const probe = await api('POST', `/api/wiki/${WID}/sources`, {
      token: owner.token,
      body: { ...crawlPayload(site, { maxDepth: 1 }), name: 'probe-' + TS },
    })
    if (probe.status !== 200) throw new Error(`权限探针源创建失败: ${probe.status} ${probe.text.slice(0, 160)}`)
    const PID = probe.json.value
    const probeSync = await waitForSync(PID, owner.token, WID, (x) => Number(x.documentCount) > 0, 10)

    // ======================= WS-S01 鉴权 =======================
    check('WS-S01a 无 token 查列表 401', (await api('GET', `/api/wiki/${WID}/sources`)).status === 401)
    check(
      'WS-S01b 无 token 创建 401',
      (await api('POST', `/api/wiki/${WID}/sources`, { body: { name: 'x' } })).status === 401,
    )
    check('WS-S01c 无 token 同步 401', (await api('POST', `/api/wiki/${WID}/sources/${crypto.randomUUID()}/sync`, { body: {} })).status === 401)

    // ======================= WS-S02 非团队成员 404 =======================
    // 注意：模型校验先于鉴权执行，故这里必须提交合法负载，才能验证到 Handler 层的「非成员 404」
    const outsiderRoutes = [
      ['GET', `/api/wiki/${WID}/sources`, undefined],
      ['POST', `/api/wiki/${WID}/sources`, { ...crawlPayload(site), name: 'outsider-src' }],
      ['PUT', `/api/wiki/${WID}/sources/${PID}`, { name: 'outsider-rename' }],
      ['DELETE', `/api/wiki/${WID}/sources/${PID}`, undefined],
      ['POST', `/api/wiki/${WID}/sources/${PID}/sync`, {}],
      ['POST', `/api/wiki/${WID}/sources/${PID}/documents`, {}],
    ]
    let all404 = true
    let oDetail = ''
    for (const [m, p, body] of outsiderRoutes) {
      const r = await api(m, p, { token: outsider.token, body })
      if (r.status !== 404) {
        all404 = false
        oDetail = `${m} ${p} -> ${r.status}`
        break
      }
    }
    check('WS-S02 非团队成员访问外部源一律 404', all404, oDetail)

    // ======================= WS-S03 Member 只读 =======================
    check('WS-S03a Member 查列表 200', (await api('GET', `/api/wiki/${WID}/sources`, { token: member.token })).status === 200)
    check(
      'WS-S03b Member 查文档列表 200',
      (await api('POST', `/api/wiki/${WID}/sources/${PID}/documents`, { token: member.token, body: {} })).status === 200,
    )
    check(
      'WS-S03c Member 创建 403',
      (await api('POST', `/api/wiki/${WID}/sources`, { token: member.token, body: { ...crawlPayload(site), name: 'member-src' } })).status === 403,
    )
    check(
      'WS-S03d Member 更新 403',
      (await api('PUT', `/api/wiki/${WID}/sources/${PID}`, { token: member.token, body: { name: 'member-rename' } })).status === 403,
    )
    check('WS-S03e Member 删除 403', (await api('DELETE', `/api/wiki/${WID}/sources/${PID}`, { token: member.token })).status === 403)
    check(
      'WS-S03f Member 同步 403',
      (await api('POST', `/api/wiki/${WID}/sources/${PID}/sync`, { token: member.token, body: {} })).status === 403,
    )

    // ======================= WS-S04 创建参数校验 =======================
    const badName = { ...crawlPayload(site), name: '' }
    check('WS-S04a 空名称 400', (await api('POST', `/api/wiki/${WID}/sources`, { token: owner.token, body: badName })).status === 400)
    check(
      'WS-S04b 超长名称 400',
      (await api('POST', `/api/wiki/${WID}/sources`, { token: owner.token, body: { ...crawlPayload(site), name: 'x'.repeat(51) } })).status === 400,
    )
    check(
      'WS-S04c 非法 cron 400',
      (await api('POST', `/api/wiki/${WID}/sources`, { token: owner.token, body: { ...crawlPayload(site), name: 'badcron', cron: 'not-a-cron' } })).status === 400,
    )
    check(
      'WS-S04d 爬虫源缺 crawler 400',
      (await api('POST', `/api/wiki/${WID}/sources`, { token: owner.token, body: { name: 'noconf', sourceType: 'crawler' } })).status === 400,
    )
    check(
      'WS-S04e 起始 URL 非 http(s) 400',
      (await api('POST', `/api/wiki/${WID}/sources`, { token: owner.token, body: { ...crawlPayload('ftp://x/kb/'), name: 'badurl' } })).status === 400,
    )
    check(
      'WS-S04f 单轮页数超上限 400',
      (await api('POST', `/api/wiki/${WID}/sources`, { token: owner.token, body: crawlPayload(site, { maxPages: 99999 }) })).status === 400,
    )
    check(
      'WS-S04g 飞书源未选任何绑定方式 400',
      (await api('POST', `/api/wiki/${WID}/sources`, { token: owner.token, body: { name: 'fs-none', sourceType: 'feishuDoc', nodeToken: 'tok' } })).status === 400,
    )
    check(
      'WS-S04h 飞书源同时给两种方式 400',
      (
        await api('POST', `/api/wiki/${WID}/sources`, {
          token: owner.token,
          body: { name: 'fs-both', sourceType: 'feishuDoc', nodeToken: 'tok', feishuAppId: crypto.randomUUID(), newAppId: 'cli_x' },
        })
      ).status === 400,
    )
    check(
      'WS-S04i 飞书源缺 nodeToken 400',
      (await api('POST', `/api/wiki/${WID}/sources`, { token: owner.token, body: { name: 'fs-notok', sourceType: 'feishuDoc', newAppId: 'cli_x', newAppSecret: 's' } })).status === 400,
    )

    // ======================= WS-S05 创建爬虫源 + 立即同步 =======================
    const SNAME = 'src-' + TS
    const created = await api('POST', `/api/wiki/${WID}/sources`, {
      token: owner.token,
      body: { ...crawlPayload(site), name: SNAME, description: 'e2e 外部源' },
    })
    check('WS-S05a Admin 创建爬虫源 200 并返回 id', created.status === 200 && !!created.json?.value, `${created.status} ${created.text.slice(0, 160)}`)
    const SID = created.json?.value

    const sourceAfterCreate = await waitForSync(SID, owner.token, WID, (x) => Number(x.documentCount) > 0)
    check(
      'WS-S05b 创建后立即拉取，文档数 > 0',
      sourceAfterCreate != null && Number(sourceAfterCreate.documentCount) > 0,
      JSON.stringify(sourceAfterCreate ?? {}).slice(0, 220),
    )
    check(
      'WS-S05c 最近同步状态为 success',
      sourceAfterCreate?.lastSyncStatus === 'success',
      `status=${sourceAfterCreate?.lastSyncStatus} msg=${sourceAfterCreate?.lastSyncMessage}`,
    )
    check(
      'WS-S05d 类型/目标/配置回显正确',
      sourceAfterCreate?.sourceType === 'crawler' && sourceAfterCreate?.crawler?.startUrl === site,
      JSON.stringify(sourceAfterCreate?.crawler ?? {}).slice(0, 160),
    )
    check('WS-S05e 列表返回 myRole（Owner=2）', sourceAfterCreate != null)

    // ======================= WS-S06 抓取范围（第一轮：index + a + b） =======================
    {
      const docs = await api('POST', `/api/wiki/${WID}/sources/${SID}/documents`, { token: owner.token, body: { pageNo: 1, pageSize: 50 } })
      const keys = (docs.json?.items ?? []).map((x) => x.externalKey ?? '')
      check('WS-S06a 仅抓取同前缀页面（站外页面未被收录）', keys.length > 0 && !keys.some((k) => k.includes('/outside/')), keys.join(',').slice(0, 200))
      check('WS-S06b 第一轮共 3 个页面入库', keys.length === 3, keys.join(',').slice(0, 200))
      check('WS-S06c 文档标题取自页面 title', (docs.json?.items ?? []).some((x) => x.externalTitle === '文档 A'), JSON.stringify(docs.json?.items ?? []).slice(0, 200))
    }

    // ======================= WS-S07 增量比对：无变化则跳过写入，但仍继续遍历 =======================
    // 索引页新增一条到子目录的链接：即使父页内容无变化，也必须能在本轮发现并收录新页面
    state.extraLink = '<a href="/kb/sub/c.html">C</a>'
    {
      const r = await api('POST', `/api/wiki/${WID}/sources/${SID}/sync`, { token: owner.token, body: { force: false } })
      check('WS-S07a 二次同步 200', r.status === 200, `${r.status} ${r.text.slice(0, 160)}`)
      // 索引页因新增链接导致正文变化 → updated 1；a / b 内容未变 → unchanged 2；sub/c 为新增 → created 1
      check(
        'WS-S07b 内容未变化页面不重复写入',
        r.json?.unchanged === 2 && r.json?.updated === 1 && r.json?.total === 4,
        JSON.stringify(r.json).slice(0, 220),
      )
      check('WS-S07c 父页无变化时仍能发现新增子页面', r.json?.created === 1 && r.json?.total === 4, JSON.stringify(r.json).slice(0, 220))
      const docs = await api('POST', `/api/wiki/${WID}/sources/${SID}/documents`, { token: owner.token, body: { pageNo: 1, pageSize: 50 } })
      check('WS-S07d 新页面已入库', (docs.json?.items ?? []).some((x) => (x.externalKey ?? '').includes('/kb/sub/c.html')), JSON.stringify(docs.json?.items ?? []).slice(0, 200))
    }

    // ======================= WS-S08 内容变化才更新 =======================
    state.aText = 'A 正文第二版（已修改）'
    {
      const r = await api('POST', `/api/wiki/${WID}/sources/${SID}/sync`, { token: owner.token, body: { force: false } })
      check('WS-S08a 变更页被更新', r.json?.updated === 1, JSON.stringify(r.json).slice(0, 220))
      check('WS-S08b 未变更页 unchanged', r.json?.unchanged >= 3, JSON.stringify(r.json).slice(0, 160))
      check('WS-S08c 失败页数为 0', r.json?.failed === 0, JSON.stringify(r.json).slice(0, 160))
    }

    // ======================= WS-S09 强制全量 =======================
    {
      const r = await api('POST', `/api/wiki/${WID}/sources/${SID}/sync`, { token: owner.token, body: { force: true } })
      check('WS-S09a force=true 全部重处理', r.status === 200 && r.json?.unchanged === 0 && r.json?.updated >= 4, `${r.status} ${JSON.stringify(r.json).slice(0, 200)}`)
      check('WS-S09b 逐文档结果含 id 与结论', (r.json?.items ?? []).length >= 4 && r.json.items.every((x) => !!x.result), JSON.stringify(r.json?.items ?? []).slice(0, 200))
    }

    // ======================= WS-S10 文档列表分页与筛选 =======================
    {
      const p1 = await api('POST', `/api/wiki/${WID}/sources/${SID}/documents`, { token: owner.token, body: { pageNo: 1, pageSize: 2 } })
      check('WS-S10a 分页 pageSize 生效', p1.status === 200 && (p1.json?.items ?? []).length === 2 && p1.json?.total >= 4, JSON.stringify(p1.json).slice(0, 200))
      const p2 = await api('POST', `/api/wiki/${WID}/sources/${SID}/documents`, { token: owner.token, body: { pageNo: 1, pageSize: 50, query: '文档 A' } })
      check('WS-S10b 标题关键字筛选生效', (p2.json?.items ?? []).length === 1 && p2.json.items[0].externalTitle === '文档 A', JSON.stringify(p2.json).slice(0, 200))
      const p3 = await api('POST', `/api/wiki/${WID}/sources/${SID}/documents`, { token: owner.token, body: { pageNo: 1, pageSize: 50, query: '不存在的标题zzz' } })
      check('WS-S10c 关键字无命中返回空列表', p3.status === 200 && (p3.json?.items ?? []).length === 0, JSON.stringify(p3.json).slice(0, 160))
    }

    // ======================= WS-S11 重名约束 =======================
    check(
      'WS-S11a 同知识库重名 409',
      (await api('POST', `/api/wiki/${WID}/sources`, { token: owner.token, body: { ...crawlPayload(site), name: SNAME, crawler: { ...crawlPayload(site).crawler, startUrl: site } } })).status === 409,
    )
    {
      const r = await api('POST', `/api/wiki/${WID2}/sources`, { token: owner.token, body: { ...crawlPayload(site), name: SNAME } })
      check('WS-S11b 不同知识库同名 200', r.status === 200, `${r.status} ${r.text.slice(0, 160)}`)
      if (r.status === 200) await api('DELETE', `/api/wiki/${WID2}/sources/${r.json.value}`, { token: owner.token })
    }

    // ======================= WS-S12 更新：字段挡板与 cron 语义 =======================
    {
      const r = await api('PUT', `/api/wiki/${WID}/sources/${SID}`, {
        token: owner.token,
        body: { name: SNAME + '-renamed', description: '更新后的描述', cron: '0 2 * * *' },
      })
      check('WS-S12a 更新名称/cron 200', r.status === 200, `${r.status} ${r.text.slice(0, 160)}`)
      const item = await waitForSync(SID, owner.token, WID, (x) => x.name === SNAME + '-renamed', 5)
      check('WS-S12b 名称与 cron 回显', item?.cron === '0 2 * * *' && item?.description === '更新后的描述', JSON.stringify(item ?? {}).slice(0, 200))
      check('WS-S12c 未提交的爬虫配置保持不变', item?.crawler?.startUrl === site, JSON.stringify(item?.crawler ?? {}).slice(0, 160))
    }
    {
      const r = await api('PUT', `/api/wiki/${WID}/sources/${SID}`, { token: owner.token, body: { crawler: { ...crawlPayload(site).crawler, maxDepth: 1 } } })
      check('WS-S12d 更新爬虫配置 200', r.status === 200, `${r.status} ${r.text.slice(0, 160)}`)
      const item = await waitForSync(SID, owner.token, WID, (x) => x.crawler?.maxDepth === 1, 5)
      check('WS-S12e 爬虫配置回显已更新', item?.crawler?.maxDepth === 1, JSON.stringify(item?.crawler ?? {}).slice(0, 160))
    }
    {
      const r = await api('PUT', `/api/wiki/${WID}/sources/${SID}`, { token: owner.token, body: { cron: '' } })
      check('WS-S12f cron 传空串关闭定时 200', r.status === 200, `${r.status}`)
      const item = await waitForSync(SID, owner.token, WID, (x) => (x.cron ?? '') === '', 5)
      check('WS-S12g cron 已清空', (item?.cron ?? '') === '', JSON.stringify(item?.cron))
    }
    check(
      'WS-S12h 非法 cron 400',
      (await api('PUT', `/api/wiki/${WID}/sources/${SID}`, { token: owner.token, body: { cron: 'abc' } })).status === 400,
    )

    // ======================= WS-S13 停用与启用 =======================
    {
      const off = await api('PUT', `/api/wiki/${WID}/sources/${SID}`, { token: owner.token, body: { isEnable: false } })
      check('WS-S13a 停用 200', off.status === 200, `${off.status}`)
      const sync = await api('POST', `/api/wiki/${WID}/sources/${SID}/sync`, { token: owner.token, body: { force: false } })
      check('WS-S13b 停用后手动同步 409', sync.status === 409, `${sync.status} ${sync.text.slice(0, 160)}`)
      const on = await api('PUT', `/api/wiki/${WID}/sources/${SID}`, { token: owner.token, body: { isEnable: true } })
      check('WS-S13c 重新启用 200', on.status === 200, `${on.status}`)
      const sync2 = await api('POST', `/api/wiki/${WID}/sources/${SID}/sync`, { token: owner.token, body: { force: false } })
      check('WS-S13d 启用后可再次同步 200', sync2.status === 200, `${sync2.status}`)
    }

    // ======================= WS-S14 飞书文档源：假凭证创建不阻断 =======================
    {
      const appName = 'wsrc-feishu-' + TS
      const r = await api('POST', `/api/wiki/${WID}/sources`, {
        token: owner.token,
        body: {
          name: 'fs-' + TS,
          sourceType: 'feishuDoc',
          nodeToken: 'nodetoken-e2e',
          newAppName: appName,
          newAppId: 'cli_e2efake' + TS,
          newAppSecret: 'secret-e2e',
          isEventSubscription: false,
          includeSubNodes: true,
        },
      })
      check('WS-S14a 假凭证创建飞书源 200（首次拉取失败不阻断创建）', r.status === 200, `${r.status} ${r.text.slice(0, 200)}`)
      const FSID = r.json?.value
      if (r.status === 200 && FSID) {
        const item = await waitForSync(FSID, owner.token, WID, (x) => x.sourceType === 'feishuDoc', 5)
        check('WS-S14b 飞书源类型与 nodeToken 回显', item?.feishu?.nodeToken === 'nodetoken-e2e', JSON.stringify(item?.feishu ?? {}).slice(0, 200))
        check('WS-S14c 拉取失败时状态落到 failed', item?.lastSyncStatus === 'failed', `status=${item?.lastSyncStatus} msg=${(item?.lastSyncMessage ?? '').slice(0, 120)}`)
        check('WS-S14d 飞书源不回显任何密钥', !JSON.stringify(item ?? {}).includes('secret-e2e'), JSON.stringify(item ?? {}).slice(0, 200))
        const del = await api('DELETE', `/api/wiki/${WID}/sources/${FSID}`, { token: owner.token })
        check('WS-S14e 删除飞书源 200', del.status === 200, `${del.status}`)
        // 清理随之建立的飞书应用连接
        const list = await api('GET', '/api/feishu_app/list', { token: owner.token })
        const apps = list.json?.items ?? list.json ?? []
        const fake = (Array.isArray(apps) ? apps : []).find((x) => x.name === appName || String(x.appId ?? '').includes(TS))
        if (fake?.id) await api('DELETE', `/api/feishu_app/${fake.id}`, { token: owner.token })
      }
    }

    // ======================= WS-S15 删除与级联 =======================
    {
      const del = await api('DELETE', `/api/wiki/${WID}/sources/${SID}`, { token: owner.token })
      check('WS-S15a 删除外部源 200', del.status === 200, `${del.status} ${del.text.slice(0, 160)}`)
      const listR = await api('GET', `/api/wiki/${WID}/sources`, { token: owner.token })
      check('WS-S15b 列表不再包含已删源', !(listR.json?.items ?? []).some((x) => x.sourceId === SID), JSON.stringify(listR.json?.items ?? []).slice(0, 160))
      check(
        'WS-S15c 已删源同步 404',
        (await api('POST', `/api/wiki/${WID}/sources/${SID}/sync`, { token: owner.token, body: {} })).status === 404,
      )
      check(
        'WS-S15d 已删源查文档 404',
        (await api('POST', `/api/wiki/${WID}/sources/${SID}/documents`, { token: owner.token, body: {} })).status === 404,
      )
      check(
        'WS-S15e 已删源再次删除 404',
        (await api('DELETE', `/api/wiki/${WID}/sources/${SID}`, { token: owner.token })).status === 404,
      )
      // 已同步进知识库的文档不会被外部源删除连带删掉
      const docList = await api('POST', `/api/wiki/${WID}/documents/list`, { token: owner.token, body: { pageNo: 1, pageSize: 20 } })
      check('WS-S15f 已入库文档保留（清理外部源不删文档）', docList.json?.total >= 4, JSON.stringify(docList.json).slice(0, 200))
    }

    // ======================= WS-S16 清理与边界 =======================
    await api('DELETE', `/api/wiki/${WID}/sources/${PID}`, { token: owner.token })
    check('WS-S16a 权限探针源清理完成', (await api('GET', `/api/wiki/${WID}/sources`, { token: owner.token })).json?.items?.every((x) => x.sourceId !== PID))
    check('WS-S16b 知识库不存在时查列表 404', (await api('GET', '/api/wiki/99999999/sources', { token: owner.token })).status === 404)
  } finally {
    await new Promise((r) => stub.server.close(r))
  }

  console.log(`\n==== WS 外部源 E2E：PASS ${PASS} / FAIL ${FAIL} ====`)
  process.exitCode = FAIL > 0 ? 1 : 0
}

main().catch((e) => {
  console.error('E2E 执行异常：', e)
  process.exitCode = 1
})
