// 墨迹天气插件「桩服务」端到端（场景 @DYN-S43 ~ @DYN-S47）
// 做法：本地起一个墨迹天气 API 桩服务（阿里云云市场网关报文形态）→ 用 MoAI__MojiWeather__Endpoint
//       启动一个独立后端 → 创建 moji_weather 实例并运行 → 断言「动态插件实例 → 注册表模板 →
//       Refit 客户端（表单编码 + APPCODE 头）→ 桩服务 → 响应容错解析」整条链路，
//       无需真实 AppCode、不消耗云市场额度。
// 前置：宿主已构建（dotnet build src/MoAI/MoAI.csproj）；postgres / redis / rabbitmq / minio 已就绪
//       （后端环境变量取自 local-dev/system.local.json 拍平，与 :5210 实例共用同一套基础设施）。
// 用法：node local-dev/moji-weather-e2e.mjs
// 环境变量：MWE_BACKEND_PORT（默认 5197）｜MWE_MOCK_PORT（默认 5196）｜MWE_KEEP_BACKEND=1 保留后端供排查
import { createServer } from 'node:http'
import { spawn } from 'node:child_process'
import crypto from 'node:crypto'
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const REPO_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const BACKEND_PORT = Number(process.env.MWE_BACKEND_PORT ?? 5197)
const MOCK_PORT = Number(process.env.MWE_MOCK_PORT ?? 5196)
const BASE = `http://127.0.0.1:${BACKEND_PORT}`
const GOOD_CODE = 'mock-appcode'
const GOOD_TOKEN = 'mock-token'

let PASS = 0, FAIL = 0
const check = (name, cond, detail = '') => {
  if (cond) { PASS++; console.log(`PASS | ${name}`) }
  else { FAIL++; console.log(`FAIL | ${name} ${detail ? '— ' + detail : ''}`) }
}

// ---------------- 墨迹天气桩服务：按云市场网关样例报文返回 ----------------
const BROADCAST_RESPONSE = {
  code: 0,
  msg: 'success',
  data: {
    city: { cityId: 285, name: '朝阳区', pname: '北京市', counname: '中国' },
    condition: {
      humidity: '20',
      icon: '0',
      pressure: '1013',
      temp: '12',
      text: '晴',
      upDateTime: '2026-09-18T10:00:00+08:00',
      windDir: '西北风',
      windLevel: '3',
      windSpeed: '15',
    },
    forecast: [
      {
        date: '2026-09-18', week: '周五',
        conditionDay: '晴', conditionNight: '多云', tempDay: '26', tempNight: '14',
        windDirDay: '西南风', windLevelDay: '1-3', windDirNight: '北风', windLevelNight: '1-3',
        sunRise: '06:00', sunSet: '18:03',
      },
      {
        date: '2026-09-19', week: '周六',
        conditionDay: '多云', conditionNight: '阴', tempDay: '24', tempNight: '15',
        windDirDay: '南风', windLevelDay: '1-3', windDirNight: '南风', windLevelNight: '1-3',
        sunRise: '06:01', sunSet: '18:01',
      },
      // 形态不认识的空对象条目应被丢弃而不是产出全空预报日
      {},
    ],
  },
}

let lastMockRequest = null
let mockHits = 0

const sleep = (ms) => new Promise((r) => setTimeout(r, ms))

function startMock() {
  const server = createServer((req, res) => {
    let body = ''
    req.on('data', (c) => { body += c })
    req.on('end', () => {
      // 请求行可能是 origin-form 或 absolute-form（本机设了 http_proxy 时），统一取 pathname
      const pathName = new URL(req.url ?? '/', 'http://127.0.0.1').pathname
      const send = (code, obj) => {
        res.writeHead(code, { 'Content-Type': 'application/json' })
        res.end(JSON.stringify(obj))
      }

      if (!pathName.startsWith('/whapi/json/aliweather/broadcast')) {
        return send(404, { code: 404, msg: `no mock for ${pathName}` })
      }

      mockHits++
      // 墨迹云市场网关为表单编码请求
      const form = Object.fromEntries(new URLSearchParams(body))
      lastMockRequest = {
        path: pathName,
        auth: req.headers['authorization'] ?? '',
        contentType: req.headers['content-type'] ?? '',
        form,
      }

      if (req.headers['authorization'] !== `APPCODE ${GOOD_CODE}`) {
        return send(401, { code: 401, msg: 'Invalid AppCode' })
      }
      // 魔法 cityId：覆盖 HTTP 200 但信封 code != 0 的失败分支
      if (form.cityId === 'bad_env') {
        return send(200, { code: 5, msg: 'param error: cityId' })
      }
      return send(200, BROADCAST_RESPONSE)
    })
  })
  return new Promise((resolve) => server.listen(MOCK_PORT, '127.0.0.1', () => resolve(server)))
}

// 把 local-dev/system.local.json 拍平成 MoAI__ 环境变量（后端不用 MAI_FILE，避免文件源覆盖脚本注入的端口/端点覆盖项）
function flattenSystemConfig() {
  const file = path.join(REPO_ROOT, 'local-dev/system.local.json')
  if (!fs.existsSync(file)) return {}
  const root = JSON.parse(fs.readFileSync(file, 'utf8'))
  const out = {}
  const walk = (obj, prefix) => {
    for (const [k, v] of Object.entries(obj ?? {})) {
      const key = prefix ? `${prefix}__${k}` : k
      if (v && typeof v === 'object' && !Array.isArray(v)) walk(v, key)
      else out[`MoAI__${key}`] = String(v)
    }
  }
  walk(root.MoAI, '')
  return out
}

function startBackend() {
  const child = spawn('dotnet', ['run', '--project', 'src/MoAI/MoAI.csproj', '--no-build', '--no-restore'], {
    cwd: REPO_ROOT,
    env: {
      ...process.env,
      ...flattenSystemConfig(),
      ASPNETCORE_ENVIRONMENT: 'Development',
      MoAI__Port: String(BACKEND_PORT),
      MoAI__MojiWeather__Endpoint: `http://127.0.0.1:${MOCK_PORT}`,
    },
    stdio: ['ignore', 'pipe', 'pipe'],
    detached: process.platform !== 'win32',
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
    if (process.platform === 'win32') {
      spawn('taskkill', ['/pid', String(child.pid), '/T', '/F'], { stdio: 'ignore' })
        .on('close', () => resolve())
    } else {
      // unix 下 kill 整个进程组（dotnet run 会再拉起宿主子进程）
      try { process.kill(-child.pid, 'SIGTERM') } catch { child.kill('SIGTERM') }
      const timer = setTimeout(() => {
        try { process.kill(-child.pid, 'SIGKILL') } catch { /* 已退出 */ }
        resolve()
      }, 8000)
      timer.unref()
      child.on('exit', () => { clearTimeout(timer); resolve() })
    }
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
  console.log(`后端启动中：${BASE}（MoAI__MojiWeather__Endpoint=http://127.0.0.1:${MOCK_PORT}）`)

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
    const MJ = `mock_moji_${TS}`
    const MJ_PLAIN = `mock_moji_nt_${TS}` // 未配置 Token 的实例
    const BAD = `mock_moji_bad_${TS}`

    // ---------- 模板出现在注册表 ----------
    const templates = await api('GET', '/api/ai/plugin', { token: admin })
    const mojiTpl = (templates.json?.items ?? []).find((x) => x.key === 'moji_weather')
    check('@DYN-S43a moji_weather 模板出现在注册表', !!mojiTpl, JSON.stringify((templates.json?.items ?? []).map((x) => x.key)))
    check('@DYN-S43b 模板带配置与参数示例', !!mojiTpl && mojiTpl.isDynamic === true && /AppCode/.test(mojiTpl.configExample ?? '') && /CityId/.test(mojiTpl.paramsExample ?? ''), JSON.stringify(mojiTpl ?? {}))

    // ---------- CityId 定位：真实走完 Refit（表单+APPCODE 头）→ 桩服务 → 容错解析 ----------
    check('创建 moji_weather 示例实例（含 Token）', (await save(admin, { pluginKey: MJ, templeteKey: 'moji_weather', title: '桩服务 墨迹天气', description: 'mock', classifyId: 0, config: JSON.stringify({ AppCode: GOOD_CODE, Token: GOOD_TOKEN }) })).status === 200)
    const mj = await run(admin, MJ, JSON.stringify({ CityId: ' 285 ' }))
    check('@DYN-S44a 墨迹天气运行成功', mj.json?.success === true, `${mj.status} ${mj.text.slice(0, 240)}`)
    check('@DYN-S44b 实况解析（温度/现象/湿度/风向/更新时间）', mj.data?.Condition?.Temp === '12' && mj.data.Condition.Text === '晴' && mj.data.Condition.Humidity === '20' && mj.data.Condition.WindDir === '西北风' && mj.data.Condition.UpDateTime === '2026-09-18T10:00:00+08:00', JSON.stringify(mj.data?.Condition ?? {}))
    check('@DYN-S44c 逐日预报解析且空对象条目被丢弃', mj.data?.Forecast?.length === 2 && mj.data.Forecast[0].Date === '2026-09-18' && mj.data.Forecast[0].TempDay === '26' && mj.data.Forecast[1].Date === '2026-09-19' && mj.data.Forecast[1].SunSet === '18:01', JSON.stringify(mj.data?.Forecast ?? []))
    check('@DYN-S44d 定位城市解析（省/区名）', mj.data?.City?.Province === '北京市' && mj.data.City.Name === '朝阳区' && mj.data.City.CityId === '285', JSON.stringify(mj.data?.City ?? {}))
    check('@DYN-S44e 请求为表单编码且 cityId 去空格 + Token 随表单下发', /application\/x-www-form-urlencoded/.test(lastMockRequest?.contentType ?? '') && lastMockRequest?.form?.cityId === '285' && lastMockRequest?.form?.token === GOOD_TOKEN && lastMockRequest?.form?.lat === undefined, JSON.stringify(lastMockRequest ?? {}))
    check('@DYN-S44f 鉴权头为 APPCODE <配置中的 AppCode>', lastMockRequest?.auth === `APPCODE ${GOOD_CODE}`, lastMockRequest?.auth ?? '')

    // ---------- 经纬度定位 + 未配置 Token 时不发 token 字段 ----------
    check('创建未配置 Token 的实例', (await save(admin, { pluginKey: MJ_PLAIN, templeteKey: 'moji_weather', title: '桩服务 墨迹天气(无Token)', description: 'mock', classifyId: 0, config: JSON.stringify({ AppCode: GOOD_CODE }) })).status === 200)
    const mj2 = await run(admin, MJ_PLAIN, JSON.stringify({ Lat: '39.90598', Lon: '116.39139' }))
    check('@DYN-S45a 经纬度定位运行成功', mj2.json?.success === true && mj2.data?.Condition?.Text === '晴', `${mj2.status} ${mj2.text.slice(0, 240)}`)
    check('@DYN-S45b lat/lon 表单下发且不带 cityId/token', lastMockRequest?.form?.lat === '39.90598' && lastMockRequest?.form?.lon === '116.39139' && lastMockRequest?.form?.cityId === undefined && lastMockRequest?.form?.token === undefined, JSON.stringify(lastMockRequest?.form ?? {}))

    // ---------- 定位参数缺失被拒 ----------
    const none = await run(admin, MJ, JSON.stringify({}))
    check('@DYN-S46a 定位参数缺失（无 CityId 且无 Lat/Lon）返回可读失败', none.json?.success === false && /CityId 或 Lat\+Lon/.test(none.json?.error ?? ''), `${none.status} ${none.json?.error ?? ''}`)
    const half = await run(admin, MJ, JSON.stringify({ Lat: '39.9' }))
    check('@DYN-S46b 只给 Lat 不给 Lon 同样被拒', half.json?.success === false && /CityId 或 Lat\+Lon/.test(half.json?.error ?? ''), half.json?.error ?? '')

    // ---------- 上游错误归一 ----------
    const env = await run(admin, MJ, JSON.stringify({ CityId: 'bad_env' }))
    const envErr = env.json?.error ?? ''
    console.log(`INFO | 信封错误失败信息：${envErr.slice(0, 240)}`)
    check('@DYN-S47a HTTP 200 但信封 code!=0 归一为可读失败（带 code 与 msg）', env.json?.success === false && /code=5/.test(envErr) && /param error/.test(envErr), envErr.slice(0, 200))

    await save(admin, { pluginKey: BAD, templeteKey: 'moji_weather', title: '桩服务 无效AppCode', description: 'mock', classifyId: 0, config: JSON.stringify({ AppCode: 'wrong-code' }) })
    const bad = await run(admin, BAD, JSON.stringify({ CityId: '285' }))
    const badErr = bad.json?.error ?? ''
    console.log(`INFO | 无效 AppCode 失败信息：${badErr.slice(0, 240)}`)
    check('@DYN-S47b 上游 401 归一为可读失败且带响应体', bad.json?.success === false && /HTTP 401/.test(badErr) && /Invalid AppCode/.test(badErr), `${bad.status} ${badErr.slice(0, 200)}`)

    // 4 次业务调用：CityId / lat+lon / bad_env 信封错误 / 无效 AppCode；
    // 两条定位缺失用例在参数校验层被拒，不应发出上游请求。
    check('桩服务命中数与业务调用一致（参数校验失败不外呼）', mockHits === 4, String(mockHits))

    // ---------- 清理 ----------
    await del(admin, MJ)
    await del(admin, MJ_PLAIN)
    await del(admin, BAD)

    console.log(`\n=== 墨迹天气插件桩服务 E2E: PASS ${PASS} / FAIL ${FAIL} ===`)
    if (FAIL > 0) process.exitCode = 1
  } finally {
    if (process.env.MWE_KEEP_BACKEND === '1') console.log(`MWE_KEEP_BACKEND=1，保留后端 pid=${backend.pid}`)
    else await killBackend(backend)
    mock.close()
  }
}

main().catch((e) => { console.error('E2E 异常:', e.message); process.exitCode = 1 })
