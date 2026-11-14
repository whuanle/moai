// 沙箱资源上限与出站网络策略 E2E（场景 @SB-Sn / @SET-S20+ / @SET-S93+ / @AP-S48 / @AP-S125+）
// 覆盖：管理员在系统设置配置每个应用沙箱最大存活时间 / CPU / 内存上限与出站网络管控模式（app/deny/intranet）+ 内网网段；
//       团队保存应用配置（agent-config）时资源不得超出、出站规则（目标格式 / 端口约束 / 规则数）与管控模式强校验。
// 用法：node local-dev/sandbox-limits-e2e.mjs [baseUrl]（默认 http://127.0.0.1:5000）
// 前置：后端运行中；种子账号 admin / abcd123456（root）。脚本结束时把上限与网络管控设置还原为默认值。
import crypto from 'node:crypto'

const BASE = process.env.APP_BASE ?? process.argv[2] ?? 'http://127.0.0.1:5000'
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

async function rootLogin() {
  const l = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
  return l.json?.accessToken ?? null
}

const saveSetting = (token, key, value) => api('PUT', '/api/settings', { token, body: { key, value } })
// 配置保存强校验要求对话模型必填：造一个授权团队的私有桩模型（main 内赋值，putSandbox 引用）
let CFG_MODEL_ID = ''
const putSandbox = (token, appId, sandbox) =>
  api('PUT', `/api/app/${appId}/agent-config`, { token, body: { prompt: '你是沙箱测试助手', modelId: CFG_MODEL_ID, wikiIds: [], plugins: [], executionSettings: { sandbox } } })

async function main() {
  const si = await api('GET', '/api/common/serverinfo')
  RSA_KEY = si.json.rsaPublic

  const root = await rootLogin()
  check('SB-00 root 登录', Boolean(root))

  // ============ 设置项读写 ============
  // SB-01 沙箱上限查询端点：登录用户可访问（应用配置页的取值范围来源）
  const unauth = await api('GET', '/api/app/sandbox-limits')
  check('SB-01a 未登录查沙箱上限 401', unauth.status === 401)
  {
    const r = await api('GET', '/api/app/sandbox-limits', { token: root })
    check('SB-01b 登录用户查沙箱上限 200 且含三项',
      r.status === 200 && Number.isFinite(r.json?.maxTtlSeconds) && typeof r.json?.maxCpu === 'string' && typeof r.json?.maxMemory === 'string',
      `${r.status} ${r.text.slice(0, 140)}`)
    check('SB-01c 上限响应含网络管控模式与内网网段',
      r.json?.networkMode === 'app' && typeof r.json?.intranetCidrs === 'string' && r.json.intranetCidrs.includes('10.0.0.0/8'),
      `${r.text.slice(0, 160)}`)
  }

  // SB-02 设置保存校验：格式非法直接 400
  check('SB-02a TTL 低于下限 60 保存 400', (await saveSetting(root, 'SANDBOX_MAX_TTL_SECONDS', '30')).status === 400)
  check('SB-02b TTL 高于上限 604800 保存 400', (await saveSetting(root, 'SANDBOX_MAX_TTL_SECONDS', '700000')).status === 400)
  check('SB-02c CPU 格式非法保存 400', (await saveSetting(root, 'SANDBOX_MAX_CPU', 'fast')).status === 400)
  check('SB-02d 内存格式非法保存 400', (await saveSetting(root, 'SANDBOX_MAX_MEMORY', '8gi')).status === 400)
  check('SB-02e 网络管控模式非法值 400', (await saveSetting(root, 'SANDBOX_NETWORK_MODE', 'allow_all')).status === 400)
  check('SB-02f 内网网段非法值 400', (await saveSetting(root, 'SANDBOX_INTRANET_CIDRS', '10.0.0.0/8,999.999.1.1')).status === 400)
  check('SB-02g 禁用命令清单含非法正则模式 400', (await saveSetting(root, 'SANDBOX_DENIED_COMMANDS', 'shutdown\nregex:([bad')).status === 400)
  check('SB-02h 禁用命令清单超 100 条 400', (await saveSetting(root, 'SANDBOX_DENIED_COMMANDS', Array.from({ length: 101 }, (_, i) => `cmd${i}`).join('\n'))).status === 400)

  // SB-03 root 收紧上限并回读生效（含网络管控模式 intranet，供后续管控校验用例使用）
  check('SB-03a root 保存 TTL=600 200', (await saveSetting(root, 'SANDBOX_MAX_TTL_SECONDS', '600')).status === 200)
  check('SB-03b root 保存 CPU=2000m 200', (await saveSetting(root, 'SANDBOX_MAX_CPU', '2000m')).status === 200)
  check('SB-03c root 保存 MEMORY=1Gi 200', (await saveSetting(root, 'SANDBOX_MAX_MEMORY', '1Gi')).status === 200)
  check('SB-03e root 保存网络管控模式=intranet 200', (await saveSetting(root, 'SANDBOX_NETWORK_MODE', 'intranet')).status === 200)
  check('SB-03f root 保存内网网段 200', (await saveSetting(root, 'SANDBOX_INTRANET_CIDRS', '10.0.0.0/8,172.16.0.0/12')).status === 200)
  check('SB-03h root 保存平台级禁用命令清单 200', (await saveSetting(root, 'SANDBOX_DENIED_COMMANDS', 'shutdown\nmkfs\nregex:\\b(wget|curl)\\b')).status === 200)
  {
    const r = await api('GET', '/api/app/sandbox-limits', { token: root })
    check('SB-03d 沙箱上限查询反映新值', r.status === 200 && r.json?.maxTtlSeconds === 600 && r.json?.maxCpu === '2000m' && r.json?.maxMemory === '1Gi',
      `${r.status} ${r.text.slice(0, 140)}`)
  }
  {
    const r = await api('GET', '/api/app/sandbox-limits', { token: root })
    check('SB-03g 查询反映网络管控模式与内网网段', r.json?.networkMode === 'intranet' && r.json?.intranetCidrs === '10.0.0.0/8,172.16.0.0/12',
      `${r.text.slice(0, 160)}`)
    check('SB-03i 查询回显平台级禁用命令清单', typeof r.json?.deniedCommands === 'string' && r.json.deniedCommands === 'shutdown\nmkfs\nregex:\\b(wget|curl)\\b',
      `${r.text.slice(0, 200)}`)
  }

  // ============ 应用配置强校验 ============
  const owner = await mkuser('so')
  const member = await mkuser('sm')
  const TID = Number((await api('POST', '/api/team', { token: owner.token, body: { name: 'sb-team-' + TS } })).json.value)

  // 对话模型必填（保存强校验）：造一个私有模型授权本团队，专供配置保存 200 用（无对话流量）
  CFG_MODEL_ID = await (async () => {
    const adminLogin = await api('POST', '/api/auth/login', { body: { userName: 'admin', password: rsa('abcd123456') } })
    const adminToken = adminLogin.json?.accessToken ?? ''
    if (!adminToken) return ''
    const chName = `sb-cfg渠道-${TS}`
    await api('POST', '/api/ai/channel', { token: adminToken, body: { providerKey: 'openai', name: chName, protocolFamily: 'openaiChatCompletions', baseUrl: 'http://127.0.0.1:9/v1', apiKey: 'sb-cfg-key', enabled: true, description: 'sandbox-limits 配置保存桩渠道' } })
    const channels = await api('GET', '/api/ai/channel', { token: adminToken })
    const chId = String((channels.json?.items ?? []).find((c) => c.name === chName)?.id ?? '')
    const modelName = `sb-cfg-chat-${TS}`
    await api('POST', '/api/ai/model', { token: adminToken, body: { channelId: chId, meta: { modelId: modelName, name: modelName, modelKind: 'conversation', description: 'sandbox-limits 配置保存桩模型' }, enabled: true, isPublic: false } })
    const models = await api('GET', `/api/ai/model?channelId=${chId}`, { token: adminToken })
    const modelId = String((models.json?.items ?? []).find((m) => m.name === modelName)?.id ?? '')
    const cur = await api('GET', `/api/ai/model/${modelId}/authorization`, { token: adminToken })
    const teamIds = [...new Set([...(cur.json?.items ?? []).map((i) => Number(i.teamId)), TID])]
    await api('PUT', `/api/ai/model/${modelId}/authorization`, { token: adminToken, body: { teamIds } })
    return modelId
  })()
  await api('POST', `/api/team/${TID}/users`, { token: owner.token, body: { userId: member.userId, role: 0 } })
  const c = await api('POST', '/api/app', { token: owner.token, body: { teamId: TID, name: '沙箱应用', appType: 'agent' } })
  const APP_ID = String(c.json?.value ?? '')
  check('SB-04 创建 Agent 应用', c.status === 200 && /^[0-9a-f-]{36}$/i.test(APP_ID), `${c.status} ${c.text.slice(0, 120)}`)

  // SB-05 启用沙箱且超限 → 400
  {
    const r = await putSandbox(owner.token, APP_ID, { enabled: true, timeoutSeconds: 3600 })
    check('SB-05a 沙箱存活时间超限 400', r.status === 400 && (r.text.includes('600') || r.json?.detail?.includes('600')), `${r.status} ${r.text.slice(0, 140)}`)
  }
  {
    const r = await putSandbox(owner.token, APP_ID, { enabled: true, resource: { cpu: '2500m' } })
    check('SB-05b 沙箱 CPU 超限 400', r.status === 400 && (r.text.includes('2000m') || r.json?.detail?.includes('2000m')), `${r.status} ${r.text.slice(0, 140)}`)
  }
  {
    const r = await putSandbox(owner.token, APP_ID, { enabled: true, resource: { memory: '2Gi' } })
    check('SB-05c 沙箱内存超限 400', r.status === 400 && (r.text.includes('1Gi') || r.json?.detail?.includes('1Gi')), `${r.status} ${r.text.slice(0, 140)}`)
  }
  {
    const r = await putSandbox(owner.token, APP_ID, { enabled: true, resource: { cpu: 'fast' } })
    check('SB-05d 沙箱 CPU 格式非法 400', r.status === 400, `${r.status} ${r.text.slice(0, 140)}`)
  }

  // SB-05e~i 出站网络策略校验（当前网络管控模式为 intranet，一并覆盖管控拦截）
  {
    const r = await putSandbox(owner.token, APP_ID, { enabled: true, network: { defaultAction: 'deny', rules: [{ action: 'allow', target: 'not a domain!' }] } })
    check('SB-05e 出站规则目标格式非法 400', r.status === 400, `${r.status} ${r.text.slice(0, 140)}`)
  }
  {
    const r = await putSandbox(owner.token, APP_ID, { enabled: true, network: { defaultAction: 'deny', rules: [{ action: 'allow', target: 'pypi.org', ports: [443] }] } })
    check('SB-05f 域名目标带端口 400', r.status === 400, `${r.status} ${r.text.slice(0, 140)}`)
  }
  {
    const r = await putSandbox(owner.token, APP_ID, { enabled: true, network: { defaultAction: 'deny', rules: [{ action: 'allow', target: '10.0.0.0/8', ports: [0, 443] }] } })
    check('SB-05g 端口非法 400', r.status === 400, `${r.status} ${r.text.slice(0, 140)}`)
  }
  {
    const rules = Array.from({ length: 101 }, (_, i) => ({ action: 'allow', target: `host${i}.example.com` }))
    const r = await putSandbox(owner.token, APP_ID, { enabled: true, network: { defaultAction: 'deny', rules } })
    check('SB-05h 出站规则超 100 条 400', r.status === 400, `${r.status} ${r.text.slice(0, 140)}`)
  }
  {
    // 管控模式 intranet 下「默认允许」被拒绝（保存时强校验）
    const r = await putSandbox(owner.token, APP_ID, { enabled: true, network: { defaultAction: 'allow', egress: ['evil.example.com'] } })
    check('SB-05i 管控模式下默认允许 400', r.status === 400 && r.text.includes('默认拒绝'), `${r.status} ${r.text.slice(0, 140)}`)
  }
  {
    // 旧格式 egress 仍可保存（字符串数组按目标格式校验，deny 语义为放行名单）
    const r = await putSandbox(owner.token, APP_ID, { enabled: true, network: { defaultAction: 'deny', egress: ['pypi.org'] } })
    check('SB-05j 旧格式 egress 合法保存 200', r.status === 200, `${r.status} ${r.text.slice(0, 140)}`)
  }
  {
    const r = await putSandbox(owner.token, APP_ID, { enabled: true, commandFilter: { enabled: true, deny: ['rm', 'regex:([bad'] } })
    check('SB-05k 命令过滤非法正则模式 400', r.status === 400, `${r.status} ${r.text.slice(0, 140)}`)
  }
  {
    const deny = Array.from({ length: 101 }, (_, i) => `cmd${i}`)
    const r = await putSandbox(owner.token, APP_ID, { enabled: true, commandFilter: { enabled: true, deny } })
    check('SB-05l 命令过滤超 100 条 400', r.status === 400, `${r.status} ${r.text.slice(0, 140)}`)
  }
  {
    // enabled=false 时非法模式同样拒绝（结构化校验不依赖开关）
    const r = await putSandbox(owner.token, APP_ID, { enabled: true, commandFilter: { enabled: false, deny: ['regex:([bad'] } })
    check('SB-05m 未启用时非法模式仍 400', r.status === 400, `${r.status} ${r.text.slice(0, 140)}`)
  }

  // SB-06 未启用沙箱时超限值不拦截（收紧上限不阻断其他字段的保存）
  {
    const r = await putSandbox(owner.token, APP_ID, { enabled: false, timeoutSeconds: 999999, resource: { cpu: '99', memory: '999Gi' } })
    check('SB-06 未启用沙箱时超限值可暂存 200', r.status === 200, `${r.status} ${r.text.slice(0, 140)}`)
  }

  // SB-07 合法值保存并回读一致
  {
    const r = await putSandbox(owner.token, APP_ID, {
      enabled: true,
      timeoutSeconds: 600,
      renewOnAccess: true,
      resource: { cpu: '2000m', memory: '1Gi' },
      network: {
        defaultAction: 'deny',
        rules: [
          { action: 'allow', target: 'pypi.org' },
          { action: 'allow', target: '10.0.0.0/8', ports: [443, 80] },
          { action: 'deny', target: '169.254.169.254' },
        ],
      },
      commandFilter: { enabled: true, deny: ['rm', 'shutdown', 'regex:\\b(wget|curl)\\b'] },
    })
    check('SB-07a 等于上限的合法配置（含出站规则与命令过滤）保存 200', r.status === 200, `${r.status} ${r.text.slice(0, 140)}`)
    const q = await api('GET', `/api/app/${APP_ID}/agent-config`, { token: owner.token })
    const sb = q.json?.executionSettings?.sandbox
    const rules = sb?.network?.rules
    check('SB-07b 回读沙箱配置一致',
      q.status === 200 && sb?.enabled === true && sb?.timeoutSeconds === 600 && sb?.resource?.cpu === '2000m' && sb?.resource?.memory === '1Gi',
      `${q.status} ${q.text.slice(0, 160)}`)
    check('SB-07c 回读出站规则一致',
      sb?.network?.defaultAction === 'deny' && Array.isArray(rules) && rules.length === 3
        && rules[0]?.target === 'pypi.org' && rules[1]?.target === '10.0.0.0/8' && JSON.stringify(rules[1]?.ports) === '[443,80]'
        && rules[2]?.action === 'deny',
      `${q.text.slice(0, 220)}`)
    check('SB-07d 回读命令过滤一致',
      sb?.commandFilter?.enabled === true && Array.isArray(sb?.commandFilter?.deny) && sb.commandFilter.deny.length === 3 && sb.commandFilter.deny[0] === 'rm',
      `${q.text.slice(0, 260)}`)
  }

  // SB-08 既有门禁回归：Member 保存配置 403
  check('SB-08 Member 保存配置 403', (await putSandbox(member.token, APP_ID, { enabled: true, timeoutSeconds: 300 })).status === 403)

  // ============ 还原默认设置 ============
  check('SB-09 还原默认设置',
    (await saveSetting(root, 'SANDBOX_MAX_TTL_SECONDS', '86400')).status === 200
    && (await saveSetting(root, 'SANDBOX_MAX_CPU', '4')).status === 200
    && (await saveSetting(root, 'SANDBOX_MAX_MEMORY', '8Gi')).status === 200
    && (await saveSetting(root, 'SANDBOX_NETWORK_MODE', 'app')).status === 200
    && (await saveSetting(root, 'SANDBOX_INTRANET_CIDRS', '10.0.0.0/8,172.16.0.0/12,192.168.0.0/16')).status === 200
    && (await saveSetting(root, 'SANDBOX_DENIED_COMMANDS', 'shutdown\nreboot\npoweroff\nhalt\ninit\nmkfs\ndd\nfdisk\nparted\niptables\nufw\nuseradd\nuserdel\nusermod\npasswd\nvisudo\ncrontab\ninsmod')).status === 200)

  console.log(`\n总计: PASS ${PASS} / FAIL ${FAIL}`)
  if (FAIL > 0) process.exit(1)
}

main().catch((e) => { console.error('E2E 执行异常:', e); process.exit(1) })
