// 动态插件多工具 + 插件语义配置 E2E（场景 @DYN-S127~S144；后端默认 http://127.0.0.1:5000，可用 DYN_BASE 覆盖）
// 覆盖：
//   多工具动态插件（@DYN-S127~S136）：模板契约发现（tools 字段）、实例工具清单接口、单工具参数示例接口、
//     按 toolKey 运行/调试、未知 toolKey 拒绝、存量单工具插件兼容（tools 固定 run 项）、配置注释原文保留。
//   插件语义配置（@DYN-S137~S144）：使用须知/语义条目/工具元数据整体覆盖保存与回显、调用策略校验 400、
//     使用须知超限 400、条目超限 400、门禁 403 与 404。
// 前置：管理员账号 admin/abcd123456（可 SITE_E2E_ADMIN_USER/PASS 覆盖）；后端已应用 asserts/plugin_semantic.sql。
const BASE = process.env.DYN_BASE ?? 'http://127.0.0.1:5000'
let PASS = 0, FAIL = 0, SKIP = 0
const check = (name, cond, detail = '') => {
  if (cond) { PASS++; console.log(`PASS | ${name}`) }
  else { FAIL++; console.log(`FAIL | ${name} ${detail ? '— ' + detail : ''}`) }
}
const safeParse = (s) => { try { return JSON.parse(s) } catch { return null } }
import crypto from 'node:crypto'

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
  try { json = JSON.parse(text) } catch { /* 非 JSON */ }
  return { status: res.status, json, text }
}

const TS = Date.now().toString().slice(-8)
const ADMIN_USER = process.env.SITE_E2E_ADMIN_USER ?? 'admin'
const ADMIN_PASS = process.env.SITE_E2E_ADMIN_PASS ?? 'abcd123456'

const MT_KEY = `text_toolkit_mt${TS}`
const GREET_KEY = `greet_compat${TS}`

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json?.rsaPublic ?? ''
  if (!RSA_KEY) throw new Error('无法获取 serverinfo.rsaPublic')

  const admin = await login(ADMIN_USER, ADMIN_PASS)

  // ===================== 一、多工具动态插件（@DYN-S127~S136） =====================

  // @DYN-S127 模板清单带工具清单：text_toolkit 3 工具，动态单工具模板固定 run 项
  const tplList = await api('GET', '/api/ai/plugin', { token: admin })
  const toolkit = (tplList.json?.items ?? []).find((x) => x.key === 'text_toolkit')
  const greet = (tplList.json?.items ?? []).find((x) => x.key === 'dynamic_greet')
  check('MT-1 模板清单包含 text_toolkit 且带 3 工具', toolkit && Array.isArray(toolkit.tools) && toolkit.tools.length === 3,
    JSON.stringify(toolkit?.tools?.map((x) => x.key)))
  check('MT-2 工具参数示例随模板清单返回（带注释原文）', toolkit?.tools?.every((x) => (x.paramsExample ?? '').length > 0),
    JSON.stringify(toolkit?.tools?.map((x) => (x.paramsExample ?? '').slice(0, 40))))
  check('MT-3 toolCount 字段：多工具=3', toolkit?.toolCount === 3, String(toolkit?.toolCount))

  // @DYN-S130 存量单工具模板兼容：tools 固定一个 run 项
  check('MT-4 存量单工具模板 tools 固定 run 项', greet && Array.isArray(greet.tools) && greet.tools.length === 1 && greet.tools[0].key === 'run',
    JSON.stringify(greet?.tools))

  // @DYN-S128 创建实例（配置带注释，验证注释原文透传）并查询工具清单接口
  const configWithComments = `{\n// 文本长度上限（演示注释保留）\n"MaxTextLength": 10000\n}`
  const saveR = await api('POST', '/api/ai/plugin/dynamic/save', {
    token: admin,
    body: { pluginKey: MT_KEY, templeteKey: 'text_toolkit', title: '文本工具集E2E', description: '多工具演示', classifyId: 0, config: configWithComments },
  })
  check('MT-5 创建 text_toolkit 实例', saveR.status === 200, `${saveR.status} ${saveR.text.slice(0, 160)}`)

  const toolsR = await api('GET', `/api/ai/plugin/tools?key=${MT_KEY}`, { token: admin })
  const toolKeys = (toolsR.json?.items ?? []).map((x) => x.key)
  check('MT-6 工具清单接口返回 3 工具', toolsR.status === 200 && toolKeys.length === 3, `${toolsR.status} ${JSON.stringify(toolKeys)}`)
  check('MT-7 工具清单含参数示例', (toolsR.json?.items ?? []).every((x) => (x.paramsExample ?? '').length > 0))

  // @DYN-S131 单工具参数示例接口（每个工具独立端点）
  const exR = await api('GET', `/api/ai/plugin/tool/example?key=${MT_KEY}&toolKey=reverse`, { token: admin })
  check('MT-8 单工具参数示例接口（reverse）', exR.status === 200 && (exR.json?.paramsExample ?? '').includes('Text'),
    `${exR.status} ${exR.text.slice(0, 120)}`)
  const exBad = await api('GET', `/api/ai/plugin/tool/example?key=${MT_KEY}&toolKey=nope`, { token: admin })
  check('MT-9 未知工具查示例 404', exBad.status === 404, String(exBad.status))

  // @DYN-S132 按 toolKey 运行（调试）：slugify
  const runR = await api('POST', '/api/ai/plugin/run', {
    token: admin,
    body: { key: MT_KEY, requestJson: JSON.stringify({ Text: 'Hello World 2!' }), function: 'slugify' },
  })
  const slug = safeParse(runR.json?.dataJson ?? '')?.Slug
  check('MT-10 按 toolKey=slugify 运行成功', runR.status === 200 && runR.json?.success === true && slug === 'hello-world-2',
    `${runR.status} ${runR.text.slice(0, 160)}`)

  // @DYN-S133 按 toolKey 运行：word_count
  const runR2 = toolExampleRun(admin, 'word_count', { Text: 'a b\nc' })
  const wc = safeParse((await runR2).json?.dataJson ?? '')
  check('MT-11 按 toolKey=word_count 运行成功', wc?.Chars === 4 && wc?.Words === 3 && wc?.Lines === 2,
    JSON.stringify(wc))

  // @DYN-S134 未知 toolKey 拒绝（400 语义 + 错误信息含可用工具）
  const runBad = await api('POST', '/api/ai/plugin/run', {
    token: admin,
    body: { key: MT_KEY, requestJson: '{"Text":"x"}', function: 'nope' },
  })
  check('MT-12 未知 toolKey 运行失败且提示可用工具', runR.status === 200 && runBad.json?.success === false && (runBad.json?.error ?? '').includes('工具不存在'),
    `${JSON.stringify(runBad.json)}`)
  const runNoTool = await api('POST', '/api/ai/plugin/run', {
    token: admin,
    body: { key: MT_KEY, requestJson: '{"Text":"x"}' },
  })
  check('MT-13 多工具插件不带 toolKey 运行失败', runNoTool.json?.success === false && (runNoTool.json?.error ?? '').includes('工具不存在'),
    `${JSON.stringify(runNoTool.json)}`)

  // @DYN-S129 配置注释原文保留（保存→回读一致，含注释与换行）
  const listR = await api('GET', '/api/ai/plugin/manage/list', { token: admin })
  const saved = (listR.json?.items ?? []).find((x) => x.pluginName === MT_KEY)
  check('MT-14 配置注释原文保留（回读含注释）', Boolean(saved?.config) && saved.config.includes('// 文本长度上限（演示注释保留）') && saved.config.includes('\n'),
    JSON.stringify((saved?.config ?? '').slice(0, 80)))

  // @DYN-S135 存量单工具实例兼容运行：dynamic_greet 不带 toolKey 正常运行
  const greetSave = await api('POST', '/api/ai/plugin/dynamic/save', {
    token: admin,
    body: { pluginKey: GREET_KEY, templeteKey: 'dynamic_greet', title: '兼容问候', description: '', classifyId: 0, config: '{"Prefix":"Hi"}' },
  })
  const greetRun = await api('POST', '/api/ai/plugin/run', {
    token: admin,
    body: { key: GREET_KEY, requestJson: '{"Name":"MoAI"}' },
  })
  const greetMsg = safeParse(greetRun.json?.dataJson ?? '')?.Message
  check('MT-15 存量单工具实例不带 toolKey 运行成功', greetSave.status === 200 && greetRun.status === 200 && greetRun.json?.success === true && greetMsg === 'Hi MoAI',
    `${greetRun.status} ${runText(greetRun)}`)
  const greetTools = await api('GET', `/api/ai/plugin/tools?key=${GREET_KEY}`, { token: admin })
  check('MT-16 存量实例工具清单固定 run 项', greetTools.json?.items?.length === 1 && greetTools.json.items[0].key === 'run')

  // ===================== 二、插件语义配置（@DYN-S137~S146） =====================

  // 取实例的 plugin.id
  const listR2 = await api('GET', '/api/ai/plugin/manage/list', { token: admin })
  const mtRow = (listR2.json?.items ?? []).find((x) => x.pluginName === MT_KEY)
  check('SM-1 manage 列表可定位实例 plugin.id', Boolean(mtRow?.id), JSON.stringify(mtRow?.id))

  // @DYN-S137 语义配置保存：使用须知 + 条目（插件级 always + onDemand）+ 工具元数据（补充说明/策略/注解位）
  const semanticBody = {
    usageNotes: '订单业务库；orders 表存订单主单，仅允许查询。',
    notes: [
      { toolKey: '', title: 'orders 表', content: '订单主单表：order_id/uid/amount/status，createtime 为创建时间。', mode: 'always', sortOrder: 0 },
      { toolKey: 'reverse', title: '反转工具注意事项', content: '超大文本会被拒绝，先分段。', mode: 'onDemand', sortOrder: 1 },
    ],
    toolMetas: [
      { toolKey: 'slugify', extraNotes: '只接受短文本', callPolicy: JSON.stringify({ denyPatterns: ['delete\\s+from'], action: 'block' }), readOnlyHint: true, destructiveHint: false },
    ],
  }
  const saveSem = await api('PUT', `/api/ai/plugin/manage/${mtRow.id}/semantic`, { token: admin, body: semanticBody })
  check('SM-2 语义配置保存成功', saveSem.status === 200, `${saveSem.status} ${saveSem.text.slice(0, 160)}`)

  const semR = await api('GET', `/api/ai/plugin/manage/${mtRow.id}/semantic`, { token: admin })
  const sem = semR.json
  check('SM-3 语义配置回显：usageNotes', semR.status === 200 && sem?.usageNotes === semanticBody.usageNotes, semR.text.slice(0, 160))
  check('SM-4 语义配置回显：条目 2 条且形态一致', sem?.notes?.length === 2
    && sem.notes[0].mode === 'always' && sem.notes[1].mode === 'onDemand'
    && sem.notes[1].toolKey === 'reverse', JSON.stringify(sem?.notes))
  check('SM-5 语义配置回显：工具元数据与策略', sem?.toolMetas?.length === 1
    && sem.toolMetas[0].extraNotes === '只接受短文本'
    && (sem.toolMetas[0].callPolicy ?? '').includes('denyPatterns')
    && sem.toolMetas[0].readOnlyHint === true, JSON.stringify(sem?.toolMetas))
  check('SM-6 语义配置回显：工具清单为 3 项', sem?.tools?.length === 3, JSON.stringify(sem?.tools?.map((x) => x.key)))

  // @DYN-S138 整体覆盖：条目减少后旧条目被删除
  const semBody2 = { ...semanticBody, notes: [semanticBody.notes[0]] }
  await api('PUT', `/api/ai/plugin/manage/${mtRow.id}/semantic`, { token: admin, body: semBody2 })
  const semR2 = await api('GET', `/api/ai/plugin/manage/${mtRow.id}/semantic`, { token: admin })
  check('SM-7 整体覆盖：旧条目被删除', semR2.json?.notes?.length === 1, JSON.stringify(semR2.json?.notes))

  // @DYN-S139 校验：调用策略 action 非法 400
  const badPolicy = { usageNotes: '', notes: [], toolMetas: [{ toolKey: 'slugify', extraNotes: '', callPolicy: '{"denyPatterns":["a"],"action":"nuke"}', readOnlyHint: false, destructiveHint: false }] }
  const badPolicyR = await api('PUT', `/api/ai/plugin/manage/${mtRow.id}/semantic`, { token: admin, body: badPolicy })
  check('SM-8 调用策略 action 非法 400', badPolicyR.status === 400, String(badPolicyR.status))

  // @DYN-S140 校验：正则不合法 400
  const badRegex = { usageNotes: '', notes: [], toolMetas: [{ toolKey: 'slugify', extraNotes: '', callPolicy: '{"denyPatterns":["([a"],"action":"block"}', readOnlyHint: false, destructiveHint: false }] }
  const badRegexR = await api('PUT', `/api/ai/plugin/manage/${mtRow.id}/semantic`, { token: admin, body: badRegex })
  check('SM-9 调用策略正则不合法 400', badRegexR.status === 400, String(badRegexR.status))

  // @DYN-S141 校验：使用须知超限 400
  const longUsage = { usageNotes: 'x'.repeat(2500), notes: [], toolMetas: [] }
  const longUsageR = await api('PUT', `/api/ai/plugin/manage/${mtRow.id}/semantic`, { token: admin, body: longUsage })
  check('SM-10 使用须知超限 400', longUsageR.status === 400, String(longUsageR.status))

  // @DYN-S142 校验：条目内容超限 400
  const longNote = { usageNotes: '', notes: [{ toolKey: '', title: 't', content: 'x'.repeat(5000), mode: 'always', sortOrder: 0 }], toolMetas: [] }
  const longNoteR = await api('PUT', `/api/ai/plugin/manage/${mtRow.id}/semantic`, { token: admin, body: longNote })
  check('SM-11 条目内容超限 400', longNoteR.status === 400, String(longNoteR.status))

  // @DYN-S143 门禁：非管理员 403（注册普通用户直接访问语义配置）
  const user = await mkuser('semu')
  const userSem = await api('GET', `/api/ai/plugin/manage/${mtRow.id}/semantic`, { token: user.token })
  check('SM-12 非管理员访问语义配置 403', userSem.status === 403, String(userSem.status))
  const userTools = await api('GET', `/api/ai/plugin/tools?key=${MT_KEY}`, { token: user.token })
  check('SM-13 非管理员访问工具清单 403', userTools.status === 403, String(userTools.status))

  // @DYN-S144 插件不存在 404
  const sem404 = await api('GET', '/api/ai/plugin/manage/00000000-0000-0000-0000-000000000001/semantic', { token: admin })
  check('SM-14 插件不存在 404', sem404.status === 404, String(sem404.status))

  // ===================== 清理 =====================
  await api('DELETE', '/api/ai/plugin/dynamic', { token: admin, body: { pluginKey: MT_KEY } })
  await api('DELETE', '/api/ai/plugin/dynamic', { token: admin, body: { pluginKey: GREET_KEY } })

  console.log(`\n合计 PASS=${PASS} FAIL=${FAIL} SKIP=${SKIP}`)
  process.exit(FAIL > 0 ? 1 : 0)
}

function runText(r) {
  return `${r.text.slice(0, 160)}`
}

async function toolExampleRun(token, toolKey, requestObj) {
  return await api('POST', '/api/ai/plugin/run', {
    token,
    body: { key: MT_KEY, requestJson: JSON.stringify(requestObj), function: toolKey },
  })
}

async function login(userName, password) {
  const r = await api('POST', '/api/auth/login', { body: { userName, password: rsa(password) } })
  if (r.status !== 200 || !r.json?.accessToken) throw new Error(`登录 ${userName} 失败: ${r.status} ${r.text.slice(0, 160)}`)
  return r.json.accessToken
}

async function mkuser(p) {
  const name = `${p}${TS}`
  const r = await api('POST', '/api/auth/register', { body: { userName: name, email: `${name}@test.local`, nickName: name, phone: `15${TS}9${String(seq++)}`.slice(0, 11), password: rsa('Test1234') } })
  if (r.status !== 200) throw new Error(`注册 ${name} 失败: ${r.status} ${r.text.slice(0, 160)}`)
  return { name, token: await login(name, 'Test1234') }
}

let seq = 0
main().catch((e) => {
  console.error('E2E 执行失败:', e)
  process.exit(1)
})
