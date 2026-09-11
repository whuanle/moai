// 团队插件 E2E（场景 @TP-Sn；后端默认 127.0.0.1:5210，可用 TP_BASE 覆盖）
import crypto from 'node:crypto'

const BASE = process.env.TP_BASE ?? 'http://127.0.0.1:5210'
let PASS = 0, FAIL = 0
const check = (name, cond, detail = '') => {
  if (cond) { PASS++; console.log(`PASS | ${name}`) }
  else { FAIL++; console.log(`FAIL | ${name} ${detail ? '— ' + detail : ''}`) }
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
  try { json = JSON.parse(text) } catch { /* 非 JSON */ }
  return { status: res.status, json, text }
}

const TS = Date.now().toString().slice(-8)
let seq = 0
const uname = (p) => `${p}${TS}${String(seq++).padStart(2, '0')}`
const phone = () => `15${Date.now().toString().slice(-8)}${String(seq++).padStart(2, '0')}`.slice(0, 11)

async function mkuser(p) {
  const name = uname(p)
  const r = await api('POST', '/api/auth/register', { body: { userName: name, email: `${name}@test.local`, nickName: name, phone: phone(), password: rsa('Test1234') } })
  if (r.status !== 200) throw new Error(`注册 ${name} 失败: ${r.status} ${r.text.slice(0, 120)}`)
  const l = await api('POST', '/api/auth/login', { body: { userName: name, password: rsa('Test1234') } })
  return { name, userId: Number(l.json.userId), token: l.json.accessToken }
}

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const owner = await mkuser('to')
  const member = await mkuser('tm')
  const outsider = await mkuser('tx')

  const TID = Number((await api('POST', '/api/team', { token: owner.token, body: { name: 'tp-team-' + TS } })).json.value)
  await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: member.userId, role: 'member' } })

  const DYN_KEY = `tp_dyn_${TS}`

  // TP-01 无 token 查团队插件列表 401
  check('TP-01 无 token 查团队插件 401', (await api('GET', `/api/team/${TID}/plugin/list`)).status === 401)

  // TP-08 非成员 404
  check('TP-08 非成员查团队插件 404', (await api('GET', `/api/team/${TID}/plugin/list`, { token: outsider.token })).status === 404)

  // TP-09 权限：Member 创建动态实例 403
  check('TP-09a Member 创建团队动态实例 403', (await api('POST', `/api/team/${TID}/plugin/dynamic`, { token: member.token, body: { teamId: TID, instanceKey: DYN_KEY, templeteKey: 'dynamic_greet', title: 'D', description: '', config: '{}' } })).status === 403)

  // TP-09 权限：Member 删除 403/404
  check('TP-19b Member 删除返回 403/404', [403, 404].includes((await api('DELETE', `/api/team/${TID}/plugin/00000000-0000-0000-0000-000000000000`, { token: member.token })).status))

  // TP-12 创建团队动态实例（Owner）
  const dynCreate = await api('POST', `/api/team/${TID}/plugin/dynamic`, { token: owner.token, body: { teamId: TID, instanceKey: DYN_KEY, templeteKey: 'dynamic_greet', title: 'D', description: '', config: '{}' } })
  check('TP-12a Owner 创建团队动态实例 200', dynCreate.status === 200)

  // TP-13 实例 key 全局唯一：同团队同 key 视为更新，跨团队同 key 冲突 409
  const owner2 = await mkuser('t2')
  const TID2 = Number((await api('POST', '/api/team', { token: owner2.token, body: { name: 'tp-team2-' + TS } })).json.value)
  check('TP-13 跨团队重复实例 key 409', (await api('POST', `/api/team/${TID2}/plugin/dynamic`, { token: owner2.token, body: { teamId: TID2, instanceKey: DYN_KEY, templeteKey: 'dynamic_greet', title: 'D2', description: '', config: '{}' } })).status === 409)

  // TP-20 动态模板列表（成员可访问）
  const tmpl = await api('GET', `/api/team/${TID}/plugin/dynamic_templates`, { token: member.token })
  check('TP-20a 成员查询动态模板 200 且含动态模板', tmpl.status === 200 && Array.isArray(tmpl.json.items) && tmpl.json.items.some((x) => x.isDynamic === true), tmpl.text.slice(0, 160))
  check('TP-20b 非成员查询动态模板 404', (await api('GET', `/api/team/${TID}/plugin/dynamic_templates`, { token: outsider.token })).status === 404)

  // 查询列表，验证团队自有动态实例（TP-26 关联修复）
  const listAdmin = await api('GET', `/api/team/${TID}/plugin/list`, { token: owner.token })
  const dynItem = (listAdmin.json?.items ?? []).find((x) => x.kind === 'dynamic' && x.isTeamOwned === true && x.pluginName === DYN_KEY)
  check('TP-26 列表含新动态实例并带审计字段', Boolean(dynItem) && dynItem.updateTime !== undefined && dynItem.createUserName !== undefined)
  check('TP-10b 列表含响应字段', listAdmin.status === 200 && listAdmin.json.items.every((x) => x.isTeamOwned !== undefined) && listAdmin.json.myRole === 2 && listAdmin.json.canManage === true)

  if (dynItem) {
    // TP-21 函数列表
    const funcs = await api('POST', `/api/team/${TID}/plugin/${dynItem.pluginId}/functions`, { token: member.token })
    check('TP-21 成员查询函数列表 200', funcs.status === 200 && Array.isArray(funcs.json.items))
    check('TP-21b 非成员函数列表 404', (await api('POST', `/api/team/${TID}/plugin/${dynItem.pluginId}/functions`, { token: outsider.token })).status === 404)

    // TP-23 刷新 MCP：成员 403
    check('TP-23 Member 刷新 MCP 403', (await api('POST', `/api/team/${TID}/plugin/${dynItem.pluginId}/refresh_mcp`, { token: member.token })).status === 403)

    // TP-22 detail：动态非自定义 404
    check('TP-22 动态实例 detail 404', (await api('GET', `/api/team/${TID}/plugin/${dynItem.pluginId}/detail`, { token: owner.token })).status === 404)
  }

  // TP-25 成员运行团队可用动态插件（路由/权限可达，配置可能业务失败）
  const run = await api('POST', `/api/team/${TID}/plugin/run`, { token: member.token, body: { teamId: TID, key: DYN_KEY, requestJson: '{"Name":"MoAI"}' } })
  check('TP-25 成员运行团队动态插件可达 200', run.status === 200 && run.json.success !== undefined, run.text.slice(0, 160))
  check('TP-25b 非成员运行 404', (await api('POST', `/api/team/${TID}/plugin/run`, { token: outsider.token, body: { teamId: TID, key: DYN_KEY, requestJson: '{}' } })).status === 404)

  // TP-24 OpenAPI 预上传：非成员 404（不依赖真实文件）
  check('TP-24 非成员 OpenAPI 预上传 404', (await api('POST', `/api/team/${TID}/plugin/pre_upload_openapi`, { token: outsider.token, body: { teamId: TID, pluginName: 'x', fileName: 'a.json', contentType: 'application/json', fileSize: 10, shA256: 'a'.repeat(64) } })).status === 404)

  // TP-19 Owner 删除团队动态插件
  if (dynItem) {
    check('TP-19a Owner 删除团队插件 200', (await api('DELETE', `/api/team/${TID}/plugin/${dynItem.pluginId}`, { token: owner.token })).status === 200)
    const listAfter = await api('GET', `/api/team/${TID}/plugin/list`, { token: owner.token })
    check('TP-19c 删除后列表不再包含', !(listAfter.json?.items ?? []).some((x) => x.pluginId === dynItem.pluginId))
  }

  console.log(`\n=== 团队插件 E2E 通过 ${PASS} / ${PASS + FAIL} ===`)
  process.exit(FAIL === 0 ? 0 : 1)
}

main().catch((e) => { console.error('FATAL', e); process.exit(1) })
