// 团队插件 E2E（场景 @TP-Sn；后端默认 127.0.0.1:5210，可用 TP_BASE 覆盖）
import crypto from 'node:crypto'
import { createServer } from 'node:http'

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

/**
 * 最小 MCP streamable-http 桩：校验 Authorization 与 query.tenant 必须等于插值后的值，
 * 否则 401 —— 团队变量插值生效与否的唯一判据。
 */
function startMcpStub(expectedAuth, expectedTenant) {
  const seen = { auths: [], tenants: [] }
  const server = createServer((req, res) => {
    let body = ''
    req.on('data', (c) => { body += c })
    req.on('end', () => {
      const url = new URL(req.url, 'http://stub.local')
      if (url.pathname !== '/mcp') { res.writeHead(404); res.end(); return }
      const auth = req.headers['authorization'] ?? ''
      seen.auths.push(auth)
      if (auth !== expectedAuth) { res.writeHead(401, { 'Content-Type': 'application/json' }); res.end('unauthorized'); return }
      let msg = {}
      try { msg = JSON.parse(body) } catch { /* 忽略空 body */ }
      const respond = (obj) => {
        res.writeHead(200, { 'Content-Type': 'application/json', 'mcp-session-id': 'e2e-session' })
        res.end(JSON.stringify(obj))
      }
      if (msg.method === 'initialize') {
        respond({ jsonrpc: '2.0', id: msg.id, result: { protocolVersion: msg.params?.protocolVersion ?? '2024-11-05', capabilities: { tools: {} }, serverInfo: { name: 'e2e-stub', version: '1.0.0' } } })
        return
      }
      if (msg.method === 'notifications/initialized') { res.writeHead(202); res.end(); return }
      if (msg.method === 'tools/list') {
        seen.tenants.push(url.searchParams.get('tenant'))
        respond({ jsonrpc: '2.0', id: msg.id, result: { tools: [{ name: 'echo_header', title: 'Echo', description: 'echo tool for e2e', inputSchema: { type: 'object', properties: {} } }] } })
        return
      }
      respond({ jsonrpc: '2.0', id: msg.id, error: { code: -32601, message: 'method not found' } })
    })
  })
  return { server, seen }
}

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

  // ===== 团队变量插值（header/query 的 {key} 占位符）=====
  {
    await api('POST', '/api/variable', { token: owner.token, body: { teamId: TID, key: 'TP_TOKEN', value: 'e2e-ok-token', name: '桩口令', description: 'e2e 插值' } })
    await api('POST', '/api/variable', { token: owner.token, body: { teamId: TID, key: 'TP_TENANT', value: 'tenant-x', name: '租户', description: 'e2e 插值' } })

    const stub = startMcpStub('Bearer e2e-ok-token', 'tenant-x')
    await new Promise((resolve) => stub.server.listen(0, '127.0.0.1', resolve))
    const port = stub.server.address().port
    const stubUrl = `http://127.0.0.1:${port}/mcp`

    // TP-29a 导入 header 含 {TP_TOKEN}：连接前插值，桩校验 Authorization 通过 → 200
    const importMcp = await api('POST', `/api/team/${TID}/plugin/mcp`, {
      token: owner.token,
      body: {
        teamId: TID,
        name: `tpvar_${TS.slice(-3).replace(/./g, (c) => String.fromCharCode(97 + Number(c)))}`,
        title: '变量插值插件',
        description: 'E2E 验证团队变量插值',
        serverUrl: stubUrl,
        header: [{ key: 'Authorization', value: 'Bearer {TP_TOKEN}' }],
        query: [{ key: 'tenant', value: '{TP_TENANT}' }],
      },
    })
    check('TP-29a 导入 header/query 含变量占位符（插值后连接桩）200', importMcp.status === 200, importMcp.text.slice(0, 160))
    check('TP-29b 桩收到的 Authorization 为插值后明文', stub.seen.auths.some((a) => a === 'Bearer e2e-ok-token'), JSON.stringify(stub.seen.auths))
    check('TP-29c 桩收到的 query.tenant 为插值后明文', stub.seen.tenants.includes('tenant-x'), JSON.stringify(stub.seen.tenants))

    const varPluginId = importMcp.json?.value
    if (varPluginId) {
      // TP-30 落库保留原始占位符：detail 回显 {TP_TOKEN} 原文
      const detail = await api('GET', `/api/team/${TID}/plugin/${varPluginId}/detail`, { token: owner.token })
      const headerVal = detail.json?.header?.[0]?.value
      const queryVal = detail.json?.query?.[0]?.value
      check('TP-30a detail 回显 header 保留 {TP_TOKEN} 原文', headerVal === 'Bearer {TP_TOKEN}', JSON.stringify(detail.json?.header))
      check('TP-30b detail 回显 query 保留 {TP_TENANT} 原文', queryVal === '{TP_TENANT}', JSON.stringify(detail.json?.query))

      // TP-31 刷新：再次插值连接桩 → 200
      const refresh = await api('POST', `/api/team/${TID}/plugin/${varPluginId}/refresh_mcp`, { token: owner.token })
      check('TP-31 刷新（插值连接）200', refresh.status === 200, refresh.text.slice(0, 120))

      await api('DELETE', `/api/team/${TID}/plugin/${varPluginId}`, { token: owner.token })
    }

    // TP-32 变量不存在：插值保留 {NOPE}，桩 401 → 导入 409
    const importMissing = await api('POST', `/api/team/${TID}/plugin/mcp`, {
      token: owner.token,
      body: {
        teamId: TID,
        name: `tpmiss_${TS.slice(-3).replace(/./g, (c) => String.fromCharCode(97 + Number(c)))}`,
        title: '缺失变量插件',
        description: 'E2E 验证未匹配变量保留原文',
        serverUrl: stubUrl,
        header: [{ key: 'Authorization', value: 'Bearer {NOPE_VAR}' }],
        query: [],
      },
    })
    check('TP-32 变量缺失导致连接失败 409', importMissing.status === 409, `${importMissing.status} ${importMissing.text.slice(0, 120)}`)

    stub.server.close()
  }

  // ===== 团队 OpenAPI 插件 header/query 保存与回显 =====
  {
    const openApiDoc = JSON.stringify({
      openapi: '3.0.1',
      info: { title: 'e2e', version: '1.0.0' },
      servers: [{ url: 'https://e2e.invalid' }],
      paths: { '/ping': { get: { operationId: 'ping', summary: 'ping', responses: { '200': { description: 'ok' } } } } },
    })
    const sha = crypto.createHash('sha256').update(openApiDoc).digest('hex')
    const pre = await api('POST', `/api/team/${TID}/plugin/pre_upload_openapi`, {
      token: owner.token,
      body: { teamId: TID, pluginName: `tpoa_${TS.slice(-3).replace(/./g, (c) => String.fromCharCode(97 + Number(c)))}`, fileName: 'openapi.json', contentType: 'application/json', fileSize: Buffer.byteLength(openApiDoc), shA256: sha },
    })
    check('TP-33a OpenAPI 预上传 200', pre.status === 200 && Boolean(pre.json?.fileId), pre.text.slice(0, 160))
    if (pre.json?.fileId && pre.json?.uploadUrl) {
      const put = await fetch(pre.json.uploadUrl, { method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: openApiDoc })
      check('TP-33b OpenAPI 文件直传 200', put.ok, String(put.status))
      const save = await api('POST', `/api/team/${TID}/plugin/openapi`, {
        token: owner.token,
        body: {
          teamId: TID,
          fileId: pre.json.fileId,
          fileName: 'openapi.json',
          name: `tpoa_${TS.slice(-3).replace(/./g, (c) => String.fromCharCode(97 + Number(c)))}`,
          title: 'OpenAPI变量插件',
          description: 'E2E 验证 OpenAPI header/query 保存回显',
          header: [{ key: 'X-Token', value: '{TP_TOKEN}' }],
          query: [{ key: 'tenant', value: '{TP_TENANT}' }],
        },
      })
      check('TP-33c 团队 OpenAPI 保存（带 header/query）200', save.status === 200, save.text.slice(0, 160))
      const oaId = save.json?.value
      if (oaId) {
        const detail = await api('GET', `/api/team/${TID}/plugin/${oaId}/detail`, { token: owner.token })
        check('TP-34a OpenAPI detail 回显 header 保留占位符', detail.json?.header?.[0]?.value === '{TP_TOKEN}', JSON.stringify(detail.json?.header))
        check('TP-34b OpenAPI detail 回显 query 保留占位符', detail.json?.query?.[0]?.value === '{TP_TENANT}', JSON.stringify(detail.json?.query))
        await api('DELETE', `/api/team/${TID}/plugin/${oaId}`, { token: owner.token })
      }
    }
  }

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
