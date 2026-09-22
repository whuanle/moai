// 动态插件 E2E（场景 @DYN-Sn；后端默认 http://127.0.0.1:5000，可用 DYN_BASE 覆盖）
// 覆盖：实例列表/创建/编辑/运行/删除/门禁（S1~S14）+ 内置模板注册与各模板失败路径（S15/S17/S19~S21/S23~S31）
//   + SQL 只读查询守卫（S30：拒绝写操作与多条语句、放行合法只读语句）+ 插件头像（S43~S45）。
// S16/S22（博查真实检索成功）需真实 API Key 与外网，脚本内以 SKIP 标注；
// S32/S33（PostgreSQL/MySQL 真实查询成功路径）用环境变量提供连接串（PG_E2E_CONNECTION / MYSQL_E2E_CONNECTION），未提供则 SKIP。
import crypto from 'node:crypto'

const BASE = process.env.DYN_BASE ?? 'http://127.0.0.1:5000'
let PASS = 0, FAIL = 0, SKIP = 0
const check = (name, cond, detail = '') => {
  if (cond) { PASS++; console.log(`PASS | ${name}`) }
  else { FAIL++; console.log(`FAIL | ${name} ${detail ? '— ' + detail : ''}`) }
}
const skip = (name, why) => { SKIP++; console.log(`SKIP | ${name} — ${why}`) }
// dataJson 是插件结果的 JSON 文本，其内部字符串里的引号会被序列化成 \u0022 —— 对内容断言一律解析后再比对，
// 不要对转义形式写正则（易随序列化器行为变化而误报）。
const safeParse = (s) => { try { return JSON.parse(s) } catch { return null } }

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

  // kg_cypher_query 模板注册（知识图谱 Text2Cypher；DYN-S35 为历史空号不复用，S46/S47 归 moji-weather）
  const kgCypherTpl = items.find((x) => x.key === 'kg_cypher_query')
  check('DYN-S48 kg_cypher_query 模板已注册且为动态', Boolean(kgCypherTpl) && kgCypherTpl.isDynamic === true, JSON.stringify(items.map((x) => x.key)))

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

  // ---- S25 内置 JS 执行器模板出现在注册表 ----
  const jsTpl = items.find((x) => x.key === 'javascript_executor')
  check('DYN-S25a 注册表含 javascript_executor 且为动态模板', Boolean(jsTpl) && jsTpl.isDynamic === true, templates.text.slice(0, 160))
  check('DYN-S25b JS 执行器模板带配置/参数示例', Boolean(jsTpl) && /JavaScriptCode/.test(jsTpl.configExample ?? '') && /Parameters/.test(jsTpl.paramsExample ?? ''), jsTpl ? jsTpl.configExample : '')
  check('DYN-S25c JS 执行器模板配置类型已解析', (jsTpl?.configType ?? '').includes('JavaScriptExecutorConfig'), jsTpl?.configType ?? '')

  // ---- S26/S27/S28 JS 执行器实例：配置/脚本/运行返回值归一 ----
  const JS = `dyn_js_${TS}`
  const jsCode = "function run(parameter) { var obj = JSON.parse(parameter); return { id: obj.id, name: obj.name, echoed: parameter }; }"
  const jc = await save(admin, { pluginKey: JS, templeteKey: 'javascript_executor', title: 'E2E JS 执行器', description: 'e2e', classifyId: 0, config: JSON.stringify({ JavaScriptCode: jsCode }) })
  check('DYN-S27a 创建 JS 实例成功', jc.status === 200, `${jc.status} ${jc.text.slice(0, 160)}`)

  // 空 JavaScriptCode → InitAsync 校验失败
  const emptyCode = await save(admin, { pluginKey: JS, templeteKey: 'javascript_executor', title: 'E2E JS 执行器', description: 'e2e', classifyId: 0, config: '{"JavaScriptCode":""}' })
  check('DYN-S27b 空 JavaScriptCode 落库成功', emptyCode.status === 200, `${emptyCode.status} ${emptyCode.text.slice(0, 160)}`)
  const emptyCodeRun = await run(admin, JS, '{"Parameters":"{}"}')
  check('DYN-S27c 空 JavaScriptCode 运行返回可读失败', emptyCodeRun.json?.success === false && /JavaScript 代码不能为空/.test(emptyCodeRun.json?.error ?? ''), `${emptyCodeRun.status} ${emptyCodeRun.text.slice(0, 200)}`)

  // 恢复正常配置
  await save(admin, { pluginKey: JS, templeteKey: 'javascript_executor', title: 'E2E JS 执行器', description: 'e2e', classifyId: 0, config: JSON.stringify({ JavaScriptCode: jsCode }) })

  // S28a Parameters 回显（注意：单引号字符串里要写 \\" 才是 JSON 的转义引号；写成 \" 会拼出非法 JSON）
  const jsParamIn = '{"id":7,"name":"MoAI"}'
  const rJsObj = await run(admin, JS, JSON.stringify({ Parameters: jsParamIn }))
  const objData = safeParse(rJsObj.json?.dataJson ?? '')
  const objResult = safeParse(objData?.ResultJson ?? '')
  check('DYN-S26a 对象返回归一为 object', rJsObj.json?.success === true && objData?.ResultKind === 'object', `${rJsObj.status} ${rJsObj.text.slice(0, 200)}`)
  check('DYN-S26b 对象 ResultJson 为结构化 JSON 文本', objResult?.id === 7 && objResult?.name === 'MoAI' && objResult?.echoed === jsParamIn, rJsObj.json?.dataJson ?? '')
  check('DYN-S28a Parameters 字段回显入参', objData?.Parameters === jsParamIn, rJsObj.json?.dataJson ?? '')

  // S26c 不同返回类型：用不同 JS 代码覆盖 string/number/boolean/array/null/undefined
  const codeByKind = {
    string: "function run(p) { return 'hello'; }",
    number: "function run(p) { return 42; }",
    boolean: "function run(p) { return true; }",
    array: "function run(p) { return [1, 2, 3]; }",
    null: "function run(p) { return null; }",
    undefined: "function run(p) { /* no return */ }",
  }
  const kindChecks = {
    string: '"ResultKind"\\s*:\\s*"string"',
    number: '"ResultKind"\\s*:\\s*"number"',
    boolean: '"ResultKind"\\s*:\\s*"boolean"',
    array: '"ResultKind"\\s*:\\s*"array"',
    null: '"ResultKind"\\s*:\\s*"null"',
    undefined: '"ResultKind"\\s*:\\s*"undefined"',
  }
  for (const [kind, code] of Object.entries(codeByKind)) {
    await save(admin, { pluginKey: JS, templeteKey: 'javascript_executor', title: 'E2E JS 执行器', description: 'e2e', classifyId: 0, config: JSON.stringify({ JavaScriptCode: code }) })
    const rr = await run(admin, JS, '{"Parameters":"{}"}')
    const dataJson = rr.json?.dataJson ?? ''
    const regex = new RegExp(kindChecks[kind])
    check(`DYN-S26c-${kind} 返回类型归一为 ${kind}`, rr.json?.success === true && regex.test(dataJson), `${rr.status} ${rr.text.slice(0, 200)}`)
    // ResultJson 按契约恒为 JSON 文本（对象/数组为结构 JSON，标量为字符串化的标量）→ 解析后比对
    const kindData = safeParse(dataJson)
    const rj = kindData?.ResultJson ?? null
    const rjParsed = typeof rj === 'string' ? safeParse(rj) : null
    if (kind === 'string') check('DYN-S26c-string ResultJson 为字符串化标量', rj === '"hello"', dataJson)
    if (kind === 'number') check('DYN-S26c-number ResultJson 为字符串化标量', rj === '42', dataJson)
    if (kind === 'boolean') check('DYN-S26c-boolean ResultJson 为字符串化标量', rj === 'true', dataJson)
    if (kind === 'array') check('DYN-S26c-array ResultJson 为数组 JSON 文本', Array.isArray(rjParsed) && rjParsed.join(',') === '1,2,3', dataJson)
    if (kind === 'null') check('DYN-S28b null 返回时 ResultJson 为 null', rj === null, dataJson)
    if (kind === 'undefined') check('DYN-S28c undefined 返回时 ResultJson 为 null', rj === null, dataJson)
  }

  // S27d 缺 run 函数 → 必须定义 run(parameter)
  await save(admin, { pluginKey: JS, templeteKey: 'javascript_executor', title: 'E2E JS 执行器', description: 'e2e', classifyId: 0, config: '{"JavaScriptCode":"var x = 1;"}' })
  const noRun = await run(admin, JS, '{"Parameters":"{}"}')
  check('DYN-S27d 未定义 run 函数返回可读失败', noRun.json?.success === false && /必须定义 run/.test(noRun.json?.error ?? ''), `${noRun.status} ${noRun.text.slice(0, 200)}`)

  // S27e 语法错误
  await save(admin, { pluginKey: JS, templeteKey: 'javascript_executor', title: 'E2E JS 执行器', description: 'e2e', classifyId: 0, config: '{"JavaScriptCode":"function run(p) { return ;;; "}' })
  const syntax = await run(admin, JS, '{"Parameters":"{}"}')
  check('DYN-S27e 语法错误返回可读失败', syntax.json?.success === false && /JavaScript/.test(syntax.json?.error ?? '') && !/实例化失败/.test(syntax.json?.error ?? ''), `${syntax.status} ${syntax.text.slice(0, 200)}`)

  // S27f 运行时错误
  await save(admin, { pluginKey: JS, templeteKey: 'javascript_executor', title: 'E2E JS 执行器', description: 'e2e', classifyId: 0, config: '{"JavaScriptCode":"function run(p) { throw new Error(\'boom\'); }"}' })
  const runtime = await run(admin, JS, '{"Parameters":"{}"}')
  check('DYN-S27f 运行时错误返回可读失败', runtime.json?.success === false && /boom/.test(runtime.json?.error ?? ''), `${runtime.status} ${runtime.text.slice(0, 200)}`)

  await del(admin, JS)

  // ---- S29 内置 SQL 只读查询模板出现在注册表 ----
  const pgTpl = items.find((x) => x.key === 'postgres_query')
  const myTpl = items.find((x) => x.key === 'mysql_query')
  check('DYN-S29a 注册表含 postgres_query 且为动态模板', Boolean(pgTpl) && pgTpl.isDynamic === true, templates.text.slice(0, 160))
  check('DYN-S29b 注册表含 mysql_query 且为动态模板', Boolean(myTpl) && myTpl.isDynamic === true, templates.text.slice(0, 160))
  check('DYN-S29c PG 模板带连接串配置示例与 Sql 参数示例', Boolean(pgTpl) && /ConnectionString/.test(pgTpl.configExample ?? '') && /Sql/.test(pgTpl.paramsExample ?? ''), pgTpl ? pgTpl.configExample : '')
  check('DYN-S29d MySQL 模板配置类型已解析', (myTpl?.configType ?? '').includes('MysqlQueryConfig'), myTpl?.configType ?? '')

  // ---- S30 只读守卫：写操作/多条语句被拒（校验先于连接，不依赖数据库可达） ----
  const PGQ = `dyn_pg_${TS}`
  const MYQ = `dyn_my_${TS}`
  const pgSave = await save(admin, { pluginKey: PGQ, templeteKey: 'postgres_query', title: 'E2E PG 只读', description: 'e2e', classifyId: 0, config: JSON.stringify({ ConnectionString: 'Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=2', MaxRows: 3, CommandTimeoutSeconds: 5 }) })
  check('DYN-S30a 创建 PG 只读实例成功', pgSave.status === 200, `${pgSave.status} ${pgSave.text.slice(0, 160)}`)
  const mySave = await save(admin, { pluginKey: MYQ, templeteKey: 'mysql_query', title: 'E2E MySQL 只读', description: 'e2e', classifyId: 0, config: JSON.stringify({ ConnectionString: 'Server=127.0.0.1;Port=1;Database=x;Uid=x;Pwd=x;Connection Timeout=2', MaxRows: 3, CommandTimeoutSeconds: 5 }) })
  check('DYN-S30b 创建 MySQL 只读实例成功', mySave.status === 200, `${mySave.status} ${mySave.text.slice(0, 160)}`)

  const forbiddenSql = [
    ['UPDATE', "UPDATE demo SET name = 'x'"],
    ['DELETE', 'DELETE FROM demo WHERE id = 1'],
    ['INSERT', 'INSERT INTO demo (id) VALUES (1)'],
    ['TRUNCATE', 'TRUNCATE TABLE demo'],
    ['DROP', 'DROP TABLE demo'],
    ['ALTER', 'ALTER TABLE demo ADD COLUMN c int'],
    ['多语句', 'SELECT 1; SELECT 2'],
    ['WITH 内嵌 DELETE', 'WITH d AS (DELETE FROM demo RETURNING id) SELECT * FROM d'],
    ['SELECT INTO', 'SELECT 1 INTO new_table'],
    ['SET 会话变量', 'SET SESSION TRANSACTION READ WRITE'],
    ['行注释后的写操作', 'SELECT 1 -- 注释\n; DELETE FROM demo'],
    ['MySQL 可执行注释', "SELECT 1 /*!50000 INTO OUTFILE '/tmp/x' */"],
  ]
  for (const [label, sql] of forbiddenSql) {
    const r = await run(admin, PGQ, JSON.stringify({ Sql: sql }))
    const err = r.json?.error ?? ''
    check(`DYN-S30c 只读守卫拒绝 ${label}`, r.json?.success === false && /只读/.test(err) && !/实例化失败/.test(err), `${r.status} ${err.slice(0, 160)}`)
  }
  const myForbidden = await run(admin, MYQ, JSON.stringify({ Sql: 'DELETE FROM demo' }))
  check('DYN-S30d MySQL 实例同样拒绝写操作', myForbidden.json?.success === false && /只读/.test(myForbidden.json?.error ?? ''), `${myForbidden.status} ${(myForbidden.json?.error ?? '').slice(0, 160)}`)

  // 放行：合法只读语句应通过守卫；连接不可达 → 报「数据库连接失败」，即证明守卫已放行
  const allowedSql = [
    'SELECT 1',
    "select id from demo where name = 'delete from demo'",
    'SELECT "update" FROM demo',
    'WITH x AS (SELECT 1 AS a) SELECT a FROM x',
    'SHOW search_path',
    'SELECT 1;',
  ]
  for (const sql of allowedSql) {
    const r = await run(admin, PGQ, JSON.stringify({ Sql: sql }))
    const err = r.json?.error ?? ''
    check(`DYN-S30e 只读守卫放行合法查询（${sql.slice(0, 28)}）`, r.json?.success === false && /数据库连接失败/.test(err), `${r.status} ${err.slice(0, 160)}`)
  }

  // ---- S31 参数/配置不合规 ----
  const emptySql = await run(admin, PGQ, '{"Sql":"   "}')
  check('DYN-S31a 空 SQL 返回可读失败', emptySql.json?.success === false && /SQL 不能为空/.test(emptySql.json?.error ?? ''), `${emptySql.status} ${(emptySql.json?.error ?? '').slice(0, 160)}`)
  await save(admin, { pluginKey: MYQ, templeteKey: 'mysql_query', title: 'E2E MySQL 只读', description: 'e2e', classifyId: 0, config: '{"ConnectionString":""}' })
  const noConn = await run(admin, MYQ, '{"Sql":"SELECT 1"}')
  check('DYN-S31b 空连接串返回可读失败', noConn.json?.success === false && /连接字符串不能为空/.test(noConn.json?.error ?? ''), `${noConn.status} ${(noConn.json?.error ?? '').slice(0, 160)}`)

  // ---- S32/S33 真实查询成功路径（连接串由环境变量提供，避免把凭据写进仓库） ----
  const PG_CONN = process.env.PG_E2E_CONNECTION
  if (PG_CONN) {
    await save(admin, { pluginKey: PGQ, templeteKey: 'postgres_query', title: 'E2E PG 只读', description: 'e2e', classifyId: 0, config: JSON.stringify({ ConnectionString: PG_CONN, MaxRows: 3, CommandTimeoutSeconds: 15 }) })

    const okRows = await run(admin, PGQ, JSON.stringify({ Sql: 'SELECT n FROM generate_series(1, 5) AS n' }))
    const rowsJson = okRows.json?.dataJson ?? ''
    check('DYN-S32a 查询成功并返回行', okRows.json?.success === true && /"RowCount"\s*:\s*3/.test(rowsJson), `${okRows.status} ${okRows.text.slice(0, 240)}`)
    check('DYN-S32b 超过 MaxRows 被截断', /"Truncated"\s*:\s*true/.test(rowsJson), rowsJson.slice(0, 240))
    check('DYN-S32c 响应含列名与行数据', /"Columns"\s*:\s*\[\s*"n"\s*\]/.test(rowsJson) && /"n"\s*:\s*1/.test(rowsJson), rowsJson.slice(0, 240))

    const roSession = await run(admin, PGQ, JSON.stringify({ Sql: 'SHOW default_transaction_read_only' }))
    check('DYN-S32d 会话被设为只读（服务端兜底生效）', roSession.json?.success === true && /"on"/.test(roSession.json?.dataJson ?? ''), `${roSession.status} ${roSession.text.slice(0, 240)}`)

    const dupCols = await run(admin, PGQ, JSON.stringify({ Sql: 'SELECT t.id, t.id FROM (VALUES (1)) AS t(id)' }))
    check('DYN-S32e 同名列自动去重', /"id_2"/.test(dupCols.json?.dataJson ?? ''), (dupCols.json?.dataJson ?? '').slice(0, 240))

    const binCol = await run(admin, PGQ, JSON.stringify({ Sql: "SELECT '\\x00ff'::bytea AS blob, '{\"a\":1}'::jsonb AS doc, now() AS ts" }))
    const binJson = binCol.json?.dataJson ?? ''
    const binRow = safeParse(binJson)?.Rows?.[0]
    check('DYN-S32f bytea/jsonb/时间类型可序列化', binCol.json?.success === true && binRow?.blob === 'AP8=' && /"a"\s*:\s*1/.test(binRow?.doc ?? '') && /^\d{4}-\d{2}-\d{2}T/.test(binRow?.ts ?? ''), `${binCol.status} ${binJson.slice(0, 320)}`)

    const emptySet = await run(admin, PGQ, JSON.stringify({ Sql: 'SELECT 1 AS n WHERE 1 = 0' }))
    const emptyJson = emptySet.json?.dataJson ?? ''
    check('DYN-S32g 空结果集返回列名且零行', emptySet.json?.success === true && /"RowCount"\s*:\s*0/.test(emptyJson) && /"Columns"/.test(emptyJson), `${emptySet.status} ${emptyJson.slice(0, 240)}`)
  } else {
    skip('DYN-S32 PostgreSQL 只读查询成功路径', '需可达的 PostgreSQL，运行前设置 PG_E2E_CONNECTION=连接串')
  }

  const MY_CONN = process.env.MYSQL_E2E_CONNECTION
  if (MY_CONN) {
    await save(admin, { pluginKey: MYQ, templeteKey: 'mysql_query', title: 'E2E MySQL 只读', description: 'e2e', classifyId: 0, config: JSON.stringify({ ConnectionString: MY_CONN, MaxRows: 3, CommandTimeoutSeconds: 15 }) })
    const myRows = await run(admin, MYQ, JSON.stringify({ Sql: 'SELECT 1 AS n' }))
    check('DYN-S33a MySQL 查询成功返回行', myRows.json?.success === true && /"RowCount"\s*:\s*1/.test(myRows.json?.dataJson ?? ''), `${myRows.status} ${myRows.text.slice(0, 240)}`)
    const myRo = await run(admin, MYQ, JSON.stringify({ Sql: "SHOW VARIABLES LIKE 'transaction_read_only'" }))
    check('DYN-S33b MySQL 会话被设为只读', myRo.json?.success === true && /ON|"1"/.test(myRo.json?.dataJson ?? ''), `${myRo.status} ${myRo.text.slice(0, 240)}`)
  } else {
    skip('DYN-S33 MySQL 只读查询成功路径', '需可达的 MySQL，运行前设置 MYSQL_E2E_CONNECTION=连接串')
  }

  // ---- S12/S13 删除 ----
  check('DYN-S12 删除实例成功', (await del(admin, GREET)).status === 200)
  const afterDel = await instances(admin)
  check('DYN-S12b 删除后列表不再出现', !afterDel.some((x) => x.pluginName === GREET))
  check('DYN-S13 重复删除 404', (await del(admin, GREET)).status === 404)
  await del(admin, BOCHA)
  await del(admin, AISEARCH)
  await del(admin, FEISHU)
  await del(admin, DUP)
  await del(admin, PGQ)
  await del(admin, MYQ)
  await del(admin, 'dynamic_greet')

  // ---- S43~S45 插件头像（POST /ai/plugin/manage/{id}/avatar；S35 为历史空号不复用）----
  const AV = `dyn_avatar_${TS}`
  const AVC = `avatar-png-${crypto.randomUUID()}`
  const avSha = crypto.createHash('sha256').update(AVC).digest('hex')
  const avPre = await api('POST', '/api/storage/public/pre_upload_image', { token: admin, body: { fileName: 'avatar.png', contentType: 'image/png', fileSize: 16, sha256: avSha } })
  const avPut = await fetch(avPre.json.uploadUrl, { method: 'PUT', headers: { 'Content-Type': 'image/png' }, body: Buffer.alloc(16, 1) })
  const avComp = await api('POST', '/api/storage/complate_url', { token: admin, body: { fileId: avPre.json.fileId, isSuccess: true } })
  check('DYN-S43 前置：公开图片直传管线完成', avPre.status === 200 && avPut.status === 200 && avComp.status === 200, `${avPre.status}/${avPut.status}/${avComp.status}`)
  await save(admin, { pluginKey: AV, templeteKey: 'dynamic_greet', title: 'E2E 头像', description: 'e2e', classifyId: 0, config: '{"Prefix":"Hi"}' })
  const avItem = (await instances(admin)).find((x) => x.pluginName === AV)
  const avSet = await api('POST', `/api/ai/plugin/manage/${avItem.id}/avatar`, { token: admin, body: { objectKey: avPre.json.objectKey } })
  const avAfter = (await instances(admin)).find((x) => x.pluginName === AV)
  check('DYN-S43 设置头像成功且列表回读 avatarPath 一致', avSet.status === 200 && avAfter.avatarPath === avPre.json.objectKey, `${avSet.status} ${String(avAfter.avatarPath).slice(0, 48)}`)
  const avBad = await api('POST', `/api/ai/plugin/manage/${avItem.id}/avatar`, { token: admin, body: { objectKey: 'public/images/no_such_file.png' } })
  check('DYN-S44 未登记/伪 objectKey 拒绝 404', avBad.status === 404, String(avBad.status))
  const avAnon = await api('POST', `/api/ai/plugin/manage/${avItem.id}/avatar`, { body: { objectKey: avPre.json.objectKey } })
  const avMember = await api('POST', `/api/ai/plugin/manage/${avItem.id}/avatar`, { token: member.token, body: { objectKey: avPre.json.objectKey } })
  check('DYN-S45 门禁：匿名 401、普通用户 403', avAnon.status === 401 && avMember.status === 403, `anon=${avAnon.status} member=${avMember.status}`)
  await del(admin, AV)

  console.log(`\n=== 动态插件 E2E: PASS ${PASS} / FAIL ${FAIL} / SKIP ${SKIP} ===`)
  if (FAIL > 0) process.exitCode = 1
}

main().catch((e) => { console.error('E2E 异常:', e.message); process.exitCode = 1 })
