// 动态插件 E2E（场景 @DYN-Sn；后端默认 http://127.0.0.1:5000，可用 DYN_BASE 覆盖）
// 覆盖：实例列表/创建/编辑/运行/删除/门禁（S1~S14）+ 内置模板注册与博查失败路径（S15/S17/S19~S21）。
// S16/S22（博查真实检索成功）需真实 API Key 与外网，脚本内以 SKIP 标注。
import crypto from 'node:crypto'

const BASE = process.env.DYN_BASE ?? 'http://127.0.0.1:5000'
let PASS = 0, FAIL = 0, SKIP = 0
const check = (name, cond, detail = '') => {
  if (cond) { PASS++; console.log(`PASS | ${name}`) }
  else { FAIL++; console.log(`FAIL | ${name} ${detail ? '— ' + detail : ''}`) }
}
const skip = (name, why) => { SKIP++; console.log(`SKIP | ${name} — ${why}`) }

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
let seq = 0
const uname = (p) => `${p}${TS}${String(seq++).padStart(2, '0')}`
const phone = () => `15${Date.now().toString().slice(-8)}${String(seq++).padStart(2, '0')}`.slice(0, 11)

async function login(userName, password) {
  const r = await api('POST', '/api/auth/login', { body: { userName, password: rsa(password) } })
  if (r.status !== 200 || !r.json?.accessToken) throw new Error(`登录 ${userName} 失败: ${r.status} ${r.text.slice(0, 160)}`)
  return r.json.accessToken
}

async function mkuser(p) {
  const name = uname(p)
  const r = await api('POST', '/api/auth/register', { body: { userName: name, email: `${name}@test.local`, nickName: name, phone: phone(), password: rsa('Test1234') } })
  if (r.status !== 200) throw new Error(`注册 ${name} 失败: ${r.status} ${r.text.slice(0, 160)}`)
  return { name, token: await login(name, 'Test1234') }
}

const save = (token, body) => api('POST', '/api/ai/plugin/dynamic/save', { token, body })
const del = (token, pluginKey) => api('DELETE', '/api/ai/plugin/dynamic', { token, body: { pluginKey } })
const run = (token, key, requestJson) => api('POST', '/api/ai/plugin/run', { token, body: { key, requestJson } })
const instances = async (token) => {
  const r = await api('GET', '/api/ai/plugin/manage/list', { token })
  return (r.json?.items ?? []).filter((x) => x.kind === 'dynamic')
}

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic
  const admin = await login('admin', 'abcd123456')
  const member = await mkuser('dynmember')

  const GREET = `dyn_greet_${TS}`
  const BOCHA = `dyn_bocha_${TS}`
  const AISEARCH = `dyn_ai_${TS}`

  // ---- S15 内置模板出现在注册表（模板下拉数据源） ----
  const templates = await api('GET', '/api/ai/plugin', { token: admin })
  const items = templates.json?.items ?? []
  const greetTpl = items.find((x) => x.key === 'dynamic_greet')
  const bochaTpl = items.find((x) => x.key === 'bocha_web_search')
  const aiTpl = items.find((x) => x.key === 'bocha_ai_search')
  check('DYN-S15a 注册表含 dynamic_greet 且为动态模板', Boolean(greetTpl) && greetTpl.isDynamic === true, templates.text.slice(0, 160))
  check('DYN-S15b 注册表含 bocha_web_search 且为动态模板', Boolean(bochaTpl) && bochaTpl.isDynamic === true, templates.text.slice(0, 160))
  check('DYN-S15c 博查模板带配置/参数示例', Boolean(bochaTpl) && /ApiKey/.test(bochaTpl.configExample ?? '') && /Query/.test(bochaTpl.paramsExample ?? ''), bochaTpl ? `${bochaTpl.configExample}` : '')
  check('DYN-S15d 博查模板配置类型已解析', (bochaTpl?.configType ?? '').includes('BoChaWebSearchConfig'), bochaTpl?.configType ?? '')

  // ---- S19 内置 AI 搜索模板出现在注册表 ----
  check('DYN-S19a 注册表含 bocha_ai_search 且为动态模板', Boolean(aiTpl) && aiTpl.isDynamic === true, templates.text.slice(0, 160))
  check('DYN-S19b AI 搜索模板带配置/参数示例', Boolean(aiTpl) && /ApiKey/.test(aiTpl.configExample ?? '') && /Query/.test(aiTpl.paramsExample ?? '') && /Answer/.test(aiTpl.paramsExample ?? ''), aiTpl ? aiTpl.paramsExample : '')
  check('DYN-S19c AI 搜索模板配置类型已解析', (aiTpl?.configType ?? '').includes('BoChaAiSearchConfig'), aiTpl?.configType ?? '')

  // ---- S2 未创建实例时列表不含目标 key ----
  const before = await instances(admin)
  check('DYN-S2 未创建实例时列表不含目标 key', !before.some((x) => x.pluginName === GREET))

  // ---- S14 门禁：非管理员 ----
  check('DYN-S14a 非管理员新建实例 403', (await save(member.token, { pluginKey: `m_${TS}`, templeteKey: 'dynamic_greet', title: 'M', description: '', classifyId: 0, config: '{}' })).status === 403)
  check('DYN-S14b 非管理员删除实例 403', (await del(member.token, GREET)).status === 403)
  check('DYN-S14c 非管理员运行插件 403', (await run(member.token, 'dynamic_greet', '{"Name":"x"}')).status === 403)
  check('DYN-S14d 非管理员查询插件列表 403', (await api('GET', '/api/ai/plugin', { token: member.token })).status === 403)

  // ---- S6 实例 key 命名规则 ----
  for (const [label, key] of [['大写字母', 'Dyn_Bad'], ['数字开头', `1dyn${TS}`], ['超长', 'a'.repeat(31)]]) {
    const r = await save(admin, { pluginKey: key, templeteKey: 'dynamic_greet', title: 'bad', description: '', classifyId: 0, config: '{}' })
    check(`DYN-S6 非法实例 key（${label}）被拒 400`, r.status === 400, `实际 ${r.status} ${r.text.slice(0, 120)}`)
  }

  // ---- S7 模板不存在 ----
  const missTpl = await save(admin, { pluginKey: `dyn_miss_${TS}`, templeteKey: 'no_such_template', title: 'x', description: '', classifyId: 0, config: '{}' })
  check('DYN-S7 模板不存在 404', missTpl.status === 404, `实际 ${missTpl.status} ${missTpl.text.slice(0, 120)}`)

  // ---- S3 创建实例 ----
  const create = await save(admin, { pluginKey: GREET, templeteKey: 'dynamic_greet', title: 'E2E 问候', description: 'e2e', classifyId: 0, config: '{"Prefix":"Hello"}' })
  check('DYN-S3 创建实例成功', create.status === 200, `${create.status} ${create.text.slice(0, 160)}`)

  // ---- S4 实例 key 与注册表模板 key 冲突 ----
  const dupTemplate = await save(admin, { pluginKey: 'dynamic_greet', templeteKey: 'dynamic_greet', title: 'dup', description: '', classifyId: 0, config: '{}' })
  check('DYN-S4 与注册表模板 key 冲突 409', dupTemplate.status === 409, `实际 ${dupTemplate.status} ${dupTemplate.text.slice(0, 120)}`)

  // ---- S1 列表展示实例（模板 key/配置） ----
  const list = await instances(admin)
  const row = list.find((x) => x.pluginName === GREET)
  check('DYN-S1a 列表出现新建实例', Boolean(row), JSON.stringify(list.map((x) => x.pluginName)))
  check('DYN-S1b 行内带模板 key 与配置', row?.templeteKey === 'dynamic_greet' && (row?.config ?? '').includes('Hello'), JSON.stringify(row ?? {}))

  // ---- S10 运行实例（使用实例配置） ----
  const r1 = await run(admin, GREET, '{"Name":"MoAI"}')
  check('DYN-S10 运行实例返回成功且用存储配置', r1.status === 200 && r1.json?.success === true && /Hello MoAI/.test(r1.json?.dataJson ?? ''), `${r1.status} ${r1.text.slice(0, 200)}`)

  // ---- S8 编辑实例配置后运行生效（S9 实例 key 不可变） ----
  const upd = await save(admin, { pluginKey: GREET, templeteKey: 'dynamic_greet', title: 'E2E 问候 v2', description: 'e2e2', classifyId: 0, config: '{"Prefix":"Hi"}' })
  check('DYN-S8a 更新实例成功', upd.status === 200, `${upd.status} ${upd.text.slice(0, 160)}`)
  const r2 = await run(admin, GREET, '{"Name":"MoAI"}')
  check('DYN-S8b 更新后运行使用新配置', r2.json?.success === true && /Hi MoAI/.test(r2.json?.dataJson ?? ''), `${r2.status} ${r2.text.slice(0, 200)}`)
  const list2 = await instances(admin)
  const row2 = list2.find((x) => x.pluginName === GREET)
  check('DYN-S9 更新后实例 key 不变、标题已变', Boolean(row2) && row2.title === 'E2E 问候 v2', JSON.stringify(row2 ?? {}))

  // ---- S5 重复提交同一实例 key：按 upsert 处理（不新建、不报冲突） ----
  const DUP = `dyn_dup_${TS}`
  await save(admin, { pluginKey: DUP, templeteKey: 'dynamic_greet', title: 'DUP v1', description: '', classifyId: 0, config: '{"Prefix":"A"}' })
  const countBeforeDup = (await instances(admin)).length
  const dupAgain = await save(admin, { pluginKey: DUP, templeteKey: 'dynamic_greet', title: 'DUP v2', description: '', classifyId: 0, config: '{"Prefix":"B"}' })
  const listAfterDup = await instances(admin)
  check('DYN-S5 重复提交同一实例 key 走更新而非新建', dupAgain.status === 200
    && listAfterDup.length === countBeforeDup
    && listAfterDup.filter((x) => x.pluginName === DUP).length === 1
    && listAfterDup.find((x) => x.pluginName === DUP)?.title === 'DUP v2',
    `${dupAgain.status} before=${countBeforeDup} after=${listAfterDup.length}`)

  // ---- S11 运行不存在的实例 ----
  const r3 = await run(admin, `dyn_absent_${TS}`, '{"Name":"x"}')
  check('DYN-S11 运行不存在实例返回失败且提示不存在', (r3.status === 404 || r3.json?.success === false) && /不存在/.test(r3.text), `${r3.status} ${r3.text.slice(0, 160)}`)

  // ---- S17 博查实例：配置/参数不合规的失败路径 ----
  const bc = await save(admin, { pluginKey: BOCHA, templeteKey: 'bocha_web_search', title: 'E2E 博查', description: 'e2e', classifyId: 0, config: '{"ApiKey":""}' })
  check('DYN-S17a 创建博查实例成功', bc.status === 200, `${bc.status} ${bc.text.slice(0, 160)}`)
  const emptyKey = await run(admin, BOCHA, '{"Query":"阿里巴巴2024年的ESG报告","Count":5}')
  check('DYN-S17b 空 API Key 运行返回可读失败', emptyKey.json?.success === false && /API Key 不能为空/.test(emptyKey.json?.error ?? ''), `${emptyKey.status} ${emptyKey.text.slice(0, 200)}`)
  await save(admin, { pluginKey: BOCHA, templeteKey: 'bocha_web_search', title: 'E2E 博查', description: 'e2e', classifyId: 0, config: '{"ApiKey":"sk-e2e-invalid"}' })
  const emptyQuery = await run(admin, BOCHA, '{"Query":"  ","Count":5}')
  check('DYN-S17c 空 Query 运行返回可读失败', emptyQuery.json?.success === false && /Query/.test(emptyQuery.json?.error ?? ''), `${emptyQuery.status} ${emptyQuery.text.slice(0, 200)}`)

  // ---- 额外：证明 IBoChaClient 已注入且请求真的发出（无效 Key → 401 或网络错误，而非“插件实例化失败”） ----
  const badKey = await run(admin, BOCHA, '{"Query":"阿里巴巴2024年的ESG报告","Summary":true,"Count":5}')
  const badKeyErr = badKey.json?.error ?? ''
  console.log(`INFO | 无效 Key 的失败信息：${badKeyErr.slice(0, 300)}`)
  check('DYN-EXT 无效 Key 触发对外调用（非实例化失败）', badKey.json?.success === false && !/实例化失败/.test(badKeyErr) && badKeyErr.length > 0, `${badKey.status} ${badKeyErr.slice(0, 200)}`)

  // ---- S20/S21 博查 AI 搜索实例：配置/参数不合规 + 对外调用失败可读 ----
  const ac = await save(admin, { pluginKey: AISEARCH, templeteKey: 'bocha_ai_search', title: 'E2E 博查 AI 搜索', description: 'e2e', classifyId: 0, config: '{"ApiKey":""}' })
  check('DYN-S20a 创建 AI 搜索实例成功', ac.status === 200, `${ac.status} ${ac.text.slice(0, 160)}`)
  const aiEmptyKey = await run(admin, AISEARCH, '{"Query":"西瓜的功效与作用","Count":5}')
  check('DYN-S20b 空 API Key 运行返回可读失败', aiEmptyKey.json?.success === false && /API Key 不能为空/.test(aiEmptyKey.json?.error ?? ''), `${aiEmptyKey.status} ${aiEmptyKey.text.slice(0, 200)}`)

  await save(admin, { pluginKey: AISEARCH, templeteKey: 'bocha_ai_search', title: 'E2E 博查 AI 搜索', description: 'e2e', classifyId: 0, config: '{"ApiKey":"sk-e2e-invalid"}' })
  const aiEmptyQuery = await run(admin, AISEARCH, '{"Query":"   ","Count":5}')
  check('DYN-S20c 空 Query 运行返回可读失败', aiEmptyQuery.json?.success === false && /Query/.test(aiEmptyQuery.json?.error ?? ''), `${aiEmptyQuery.status} ${aiEmptyQuery.text.slice(0, 200)}`)

  // 证明 IBoChaClient 注入成功且 AI Search 请求真实发出（无效 Key → 401，而非“插件实例化失败”）
  const aiBadKey = await run(admin, AISEARCH, '{"Query":"西瓜的功效与作用","Answer":true,"Count":5}')
  const aiBadKeyErr = aiBadKey.json?.error ?? ''
  console.log(`INFO | AI 搜索无效 Key 的失败信息：${aiBadKeyErr.slice(0, 300)}`)
  check('DYN-S21 无效 Key 触发对外调用（非实例化失败）且含 HTTP 状态码', aiBadKey.json?.success === false && !/实例化失败/.test(aiBadKeyErr) && /HTTP \d{3}/.test(aiBadKeyErr), `${aiBadKey.status} ${aiBadKeyErr.slice(0, 200)}`)

  skip('DYN-S16 博查全网搜索成功路径', '本脚本不依赖真实 Key；成功路径（含响应解析）由 local-dev/bocha-search-e2e.mjs 用桩服务自动验证')
  skip('DYN-S22 博查 AI 搜索响应解析', '同上：由 local-dev/bocha-search-e2e.mjs 用桩服务自动验证')

  // ---- S23 飞书文本推送内置模板出现在注册表 ----
  const feishuTpl = items.find((x) => x.key === 'feishu_webhook_text')
  check('DYN-S23a 注册表含 feishu_webhook_text 且为动态模板', Boolean(feishuTpl) && feishuTpl.isDynamic === true, templates.text.slice(0, 160))
  check('DYN-S23b 飞书模板带配置/参数示例', Boolean(feishuTpl) && /WebhookKey/.test(feishuTpl.configExample ?? '') && /Text/.test(feishuTpl.paramsExample ?? ''), feishuTpl ? feishuTpl.paramsExample : '')
  check('DYN-S23c 飞书模板配置类型已解析', (feishuTpl?.configType ?? '').includes('FeishuWebhookTextConfig'), feishuTpl?.configType ?? '')

  // ---- S24 飞书实例：参数/配置不合规的失败路径 ----
  const FEISHU = `dyn_feishu_${TS}`
  const fc = await save(admin, { pluginKey: FEISHU, templeteKey: 'feishu_webhook_text', title: 'E2E 飞书推送', description: 'e2e', classifyId: 0, config: '{"WebhookKey":"","SignKey":""}' })
  check('DYN-S24a 创建飞书实例成功', fc.status === 200, `${fc.status} ${fc.text.slice(0, 160)}`)
  const fEmptyKey = await run(admin, FEISHU, '{"Text":"hello"}')
  check('DYN-S24b 缺 WebhookKey 运行返回可读失败', fEmptyKey.json?.success === false && /WebhookKey/.test(fEmptyKey.json?.error ?? ''), `${fEmptyKey.status} ${fEmptyKey.text.slice(0, 200)}`)
  await save(admin, { pluginKey: FEISHU, templeteKey: 'feishu_webhook_text', title: 'E2E 飞书推送', description: 'e2e', classifyId: 0, config: '{"WebhookKey":"11111111-2222-3333-4444-555555555555","SignKey":""}' })
  const fEmptyText = await run(admin, FEISHU, '{"Text":"   "}')
  check('DYN-S24c 空 Text 运行返回可读失败', fEmptyText.json?.success === false && /Text/.test(fEmptyText.json?.error ?? ''), `${fEmptyText.status} ${fEmptyText.text.slice(0, 200)}`)
  // 用占位 token 触发真实调用：飞书 webhook 对未知 hook 返回 200 + code:19001 或 404；只要不是"实例化失败"即视为对外调用成功发出
  const fBadKey = await run(admin, FEISHU, '{"Text":"ping"}')
  const fBadKeyErr = fBadKey.json?.error ?? ''
  console.log(`INFO | 飞书无效 token 的失败信息：${fBadKeyErr.slice(0, 300)}`)
  check('DYN-S24d 无效 WebhookKey 触发对外调用（非实例化失败）', fBadKey.json?.success === false && !/实例化失败/.test(fBadKeyErr) && fBadKeyErr.length > 0, `${fBadKey.status} ${fBadKeyErr.slice(0, 200)}`)

  // ---- S12/S13 删除 ----
  check('DYN-S12 删除实例成功', (await del(admin, GREET)).status === 200)
  const afterDel = await instances(admin)
  check('DYN-S12b 删除后列表不再出现', !afterDel.some((x) => x.pluginName === GREET))
  check('DYN-S13 重复删除 404', (await del(admin, GREET)).status === 404)
  await del(admin, BOCHA)
  await del(admin, AISEARCH)
  await del(admin, FEISHU)
  await del(admin, DUP)
  await del(admin, 'dynamic_greet')

  console.log(`\n=== 动态插件 E2E: PASS ${PASS} / FAIL ${FAIL} / SKIP ${SKIP} ===`)
  if (FAIL > 0) process.exitCode = 1
}

main().catch((e) => { console.error('E2E 异常:', e.message); process.exitCode = 1 })
