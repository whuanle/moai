// UX 走查：采集交互合理/不合理证据（截图 + 控制台错误 + 交互状态检查）
import { createRequire } from 'node:module'
import { mkdirSync } from 'node:fs'
const require = createRequire('/Users/wen/project/maomi/moai/ui/package.json')
const { chromium } = require('playwright')

const FRONT = 'http://localhost:4000'
const SHOTS = '/Users/wen/project/maomi/moai/local-dev/shots-ux'
mkdirSync(SHOTS, { recursive: true })

const browser = await chromium.launch()
const page = await browser.newPage({ viewport: { width: 1440, height: 900 } })
const consoleErrors = []
page.on('console', (m) => { if (m.type() === 'error') consoleErrors.push(m.text().slice(0, 160)) })
page.on('pageerror', (e) => consoleErrors.push('PAGEERROR ' + String(e).slice(0, 160)))
const shot = (n) => page.screenshot({ path: `${SHOTS}/${n}.png` })
const notes = []

// 登录
await page.goto(`${FRONT}/login`)
await page.getByPlaceholder('请输入用户名').fill('admin')
await page.getByPlaceholder('请输入密码').fill('abcd123456')
await page.getByRole('button', { name: '立即登录' }).click()
await page.waitForURL('**/dashboard')
await page.waitForTimeout(3600)
await shot('ux-01-dashboard')

// —— 用户页 UX 细查 ——
await page.getByRole('menuitem', { name: '用户' }).click()
await page.waitForURL('**/users')
await page.waitForSelector('tbody tr')

// 1) 搜索无结果时的空态
await page.getByPlaceholder(/用户名 \/ 昵称 \/ 邮箱/).fill('zzz不存在的用户')
await page.getByRole('button', { name: '查询' }).click()
await page.waitForTimeout(900)
const emptyText = await page.locator('.ant-table-placeholder, .ant-empty').first().innerText().catch(() => '')
notes.push(`空态提示文案: "${emptyText.trim().slice(0, 40)}" ${emptyText.includes('暂无数据') ? '(合理：标准空态)' : '(需人工评估)'}`)
await shot('ux-02-empty-search')
await page.getByRole('button', { name: '重置' }).click()
await page.waitForTimeout(900)

// 2) 分页器信息量
const paginationText = await page.locator('.ant-pagination').first().innerText().catch(() => '')
notes.push(`分页器文案: "${paginationText.replace(/\n/g, ' ').slice(0, 60)}" ${/共 \d+ 条/.test(paginationText) ? '(合理：含总数)' : '(无总数)'}`)

// 3) 受保护行是否有解释（root 行只显示"查看"，用户是否会困惑？）
const rootRow = page.locator('tbody tr', { hasText: 'admin' }).first()
const hasTooltip = await rootRow.locator('.ant-tooltip, [title]').count()
notes.push(`root 行危险操作隐藏${hasTooltip > 0 ? '但有提示' : '且无任何解释'} → ${hasTooltip > 0 ? '合理' : '可改进：普通行有按钮而 root 行没有，缺少"为什么不能操作"的说明'}`)

// 4) 禁用确认文案
const normalRow = page.locator('tbody tr').filter({ has: page.getByRole('button', { name: '禁用' }) }).first()
await normalRow.getByRole('button', { name: '禁用' }).click()
await page.waitForTimeout(500)
const popText = await page.locator('.ant-popover, .ant-popconfirm').first().innerText().catch(() => '')
notes.push(`禁用二次确认文案: "${popText.replace(/\n/g, ' ').slice(0, 50)}" ${popText.includes('确认') || popText.includes('？') ? '(合理：有后果说明)' : '(缺后果说明)'}`)
await shot('ux-03-disable-confirm')
await page.keyboard.press('Escape')

// 5) 重置密码弹窗强度提示
await normalRow.getByRole('button', { name: '重置密码' }).click()
await page.waitForSelector('.ant-modal:visible')
const hasRuleHint = await page.locator('.ant-modal').getByText(/8-20|字母和数字/).count()
notes.push(`重置密码弹窗强度规则提示: ${hasRuleHint > 0 ? '有（合理）' : '无（可改进：用户需提交后才知道规则）'}`)
await page.locator('.ant-modal .ant-modal-close').click()
await page.locator('.ant-modal').waitFor({ state: 'hidden' })
await page.waitForTimeout(300)

// 6) 设置页/账号页快速走查
await page.getByRole('menuitem', { name: '设置' }).click()
await page.waitForURL('**/settings')
await shot('ux-04-settings')
const switchDesc = await page.locator('.ant-card').first().innerText()
notes.push(`设置页开关带说明文字: ${switchDesc.includes('开启') || switchDesc.length > 30 ? '是（合理）' : '否'}`)

// 7) 未实现菜单项（已知问题复核）
await page.getByRole('menuitem', { name: '应用' }).click()
await page.waitForTimeout(900)
notes.push(`点击未实现的"应用"菜单后 URL=${page.url().replace(FRONT, '')} → 不合理：无任何反馈直接弹回概览（已在 layout-routing 文档记录为已知问题）`)
await shot('ux-05-ghost-menu')

// 8) 长用户名/邮箱溢出
await page.getByRole('menuitem', { name: '用户' }).click()
await page.waitForURL('**/users')
await page.waitForSelector('tbody tr')
const hasEllipsis = await page.locator('tbody tr td').filter({ hasText: '@' }).first().evaluate((el) => getComputedStyle(el).textOverflow === 'ellipsis' || el.scrollWidth > el.clientWidth).catch(() => 'n/a')
notes.push(`邮箱列超长处理: ${hasEllipsis === true ? '省略号截断（合理）' : hasEllipsis === false ? '未截断（可能溢出）' : '无长邮箱数据可判'}`)

console.log('\n===== UX 走查记录 =====')
notes.forEach((n) => console.log(' -', n))
console.log('\n===== 控制台错误（' + consoleErrors.length + ' 条）=====')
consoleErrors.slice(0, 8).forEach((e) => console.log(' *', e))
console.log(`\n截图目录: ${SHOTS}`)
await browser.close()
