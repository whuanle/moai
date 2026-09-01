#!/usr/bin/env python3
"""生成用户管理 HTML 操作手册（自包含：截图 base64 内嵌）"""
import base64, os

ROOT = '/Users/wen/project/maomi/moai'
SH1 = os.path.join(ROOT, 'local-dev/shots')
SH2 = os.path.join(ROOT, 'local-dev/shots-ux')

def b64(path):
    with open(path, 'rb') as f:
        return 'data:image/png;base64,' + base64.b64encode(f.read()).decode()

imgs = {n: b64(os.path.join(SH1, f'{n}.png')) for n in
        ['01-dashboard', '02-users', '03-user-detail', '04-users-search', '05-password-mismatch',
         '06-settings', '07-oauthconnect', '08-account', '09-dark-theme', '10-english', '11-logout']}
for n in ['ux-02-empty-search', 'ux-03-disable-confirm', 'ux-05-ghost-menu']:
    imgs[n] = b64(os.path.join(SH2, f'{n}.png'))

def fig(key, caption):
    return f'<figure><img src="{imgs[key]}" alt="{caption}"/><figcaption>{caption}</figcaption></figure>'

html = f"""<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="UTF-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>MoAI 用户管理 · 操作手册</title>
<style>
  :root {{ --brand:#2970FF; --text:#101828; --sub:#475467; --line:#E5E7EB; --bg:#F9FAFB; }}
  * {{ box-sizing:border-box; }}
  body {{ margin:0; font-family:Inter,-apple-system,BlinkMacSystemFont,'Segoe UI','PingFang SC',sans-serif; color:var(--text); background:var(--bg); line-height:1.7; }}
  .wrap {{ max-width:960px; margin:0 auto; padding:32px 20px 80px; }}
  header.hero {{ background:linear-gradient(135deg,#2970FF 0%,#1E5BFF 100%); color:#fff; border-radius:16px; padding:36px 40px; margin-bottom:32px; }}
  header.hero h1 {{ margin:0 0 8px; font-size:28px; }}
  header.hero p {{ margin:0; opacity:.9; }}
  .meta {{ font-size:13px; opacity:.85; margin-top:14px; }}
  h2 {{ font-size:20px; margin:40px 0 12px; padding-bottom:8px; border-bottom:2px solid var(--line); }}
  h2 .tag {{ font-size:12px; font-weight:600; background:#EAF1FF; color:var(--brand); border-radius:999px; padding:2px 10px; margin-left:8px; vertical-align:2px; }}
  h3 {{ font-size:16px; margin:20px 0 8px; }}
  figure {{ margin:16px 0; padding:12px; background:#fff; border:1px solid var(--line); border-radius:12px; }}
  figure img {{ width:100%; border-radius:8px; border:1px solid var(--line); }}
  figcaption {{ font-size:13px; color:var(--sub); margin-top:8px; text-align:center; }}
  ol.steps li, ul.plain li {{ margin:6px 0; }}
  .callout {{ background:#fff; border:1px solid var(--line); border-left:4px solid var(--brand); border-radius:8px; padding:12px 16px; margin:14px 0; font-size:14px; }}
  .callout.warn {{ border-left-color:#F79009; }}
  .callout.danger {{ border-left-color:#F04438; }}
  table {{ width:100%; border-collapse:collapse; background:#fff; border:1px solid var(--line); border-radius:8px; overflow:hidden; font-size:14px; margin:12px 0; }}
  th,td {{ padding:10px 12px; border-bottom:1px solid var(--line); text-align:left; vertical-align:top; }}
  th {{ background:#F2F4F7; font-weight:600; }}
  .ok {{ color:#17B26A; font-weight:600; }}
  .bad {{ color:#F04438; font-weight:600; }}
  code {{ background:#F2F4F7; border-radius:4px; padding:1px 6px; font-size:13px; }}
  .toc {{ background:#fff; border:1px solid var(--line); border-radius:12px; padding:16px 24px; font-size:14px; }}
  .toc a {{ color:var(--brand); text-decoration:none; }}
  footer {{ margin-top:48px; font-size:12px; color:var(--sub); text-align:center; }}
</style>
</head>
<body><div class="wrap">

<header class="hero">
  <h1>MoAI 用户管理 · 操作指南</h1>
  <p>管理员如何管理用户、授权共同维护者、禁用账号与重置密码</p>
  <div class="meta">适用版本：2026-09 · 截图均为真实系统实测 · 行为规格见 docs/user-management/bdd.md（场景编号 @UM-Sn）</div>
</header>

<nav class="toc">
  <b>目录：</b>
  <a href="#login">1 登录</a> · <a href="#list">2 用户列表与搜索</a> · <a href="#detail">3 查看用户信息</a> ·
  <a href="#admin">4 设为 / 取消管理员</a> · <a href="#disable">5 禁用 / 启用账号</a> · <a href="#reset">6 重置密码</a> ·
  <a href="#prefs">7 主题与语言</a> · <a href="#logout">8 退出登录</a> ·
  <a href="#roles">9 角色权限速查</a> · <a href="#faq">10 常见问题</a> · <a href="#ux">11 交互合理性评估</a>
</nav>

<h2 id="login">1. 登录 <span class="tag">@UM-S22 前置</span></h2>
<ol class="steps">
  <li>打开系统地址（本地开发为 <code>http://localhost:4000</code>），进入登录页。</li>
  <li>输入管理员账号与密码（初始超级管理员 <code>admin / abcd123456</code>，<b class="bad">上线后请立即修改</b>）。</li>
  <li>点击「立即登录」，成功后进入概览页。</li>
</ol>
{fig('01-dashboard', '图 1 · 登录成功后的概览页')}
<div class="callout">密码在浏览器端使用服务器公钥加密后传输，页面不会明文发送密码。</div>

<h2 id="list">2. 用户列表与搜索 <span class="tag">@UM-S1 @UM-S3</span></h2>
<ol class="steps">
  <li>点击左侧菜单「用户」进入用户管理页。</li>
  <li>表格展示全部用户：用户名、昵称、邮箱、手机号、<b>角色</b>（超级管理员/管理员/普通用户）、<b>状态</b>（正常/已禁用）、创建时间。</li>
  <li>顶部搜索框输入关键字（匹配用户名/昵称/邮箱）→ 点「查询」；点「重置」清空条件。</li>
</ol>
{fig('02-users', '图 2 · 用户列表：注意 admin 行的角色为「超级管理员」，且该行没有危险操作按钮')}
{fig('04-users-search', '图 3 · 搜索 "admin" 后的过滤结果')}
{fig('ux-02-empty-search', '图 4 · 搜索无结果时显示标准空态「暂无数据」')}
<div class="callout">分页器实时显示总人数（如「共 69 条」），每页条数可切换。</div>

<h2 id="detail">3. 查看用户信息 <span class="tag">@UM-S5</span></h2>
<ol class="steps">
  <li>在目标行点击「查看」。</li>
  <li>弹窗展示该用户的用户名、昵称、邮箱、手机号、角色与状态。</li>
  <li>点击右上角 ✕ 关闭弹窗。</li>
</ol>
{fig('03-user-detail', '图 5 · 用户详情弹窗')}

<h2 id="admin">4. 设为 / 取消管理员（仅超级管理员）<span class="tag">@UM-S7 @UM-S8 @UM-S9</span></h2>
<div class="callout warn"><b>前置：</b>只有超级管理员（root）能看到「设为管理员 / 取消管理员」按钮。普通管理员登录时不会出现该操作。</div>
<ol class="steps">
  <li>在目标用户行点击「设为管理员」。</li>
  <li>确认弹窗后即生效；对方<b>刷新页面或重新登录</b>后获得管理菜单。</li>
  <li>撤销：同一位置点击「取消管理员」。</li>
</ol>
<div class="callout danger"><b>保护规则（系统强制）：</b>超级管理员账号不能被任何人降级；任何人都不能操作自己的管理员角色；管理员之间不能互相授权。</div>

<h2 id="disable">5. 禁用 / 启用账号 <span class="tag">@UM-S12 ~ @UM-S17</span></h2>
<ol class="steps">
  <li>目标行点击「禁用」→ 弹出二次确认（含后果说明）→ 点「确定」。</li>
  <li>被禁用用户<b>下一个请求</b>即被系统拦截并提示「账号已被禁用」。</li>
  <li>恢复：同一行点击「启用」。</li>
</ol>
{fig('ux-03-disable-confirm', '图 6 · 禁用前的二次确认（明确说明后果）')}
<div class="callout danger">不能禁用超级管理员，也不能禁用自己；普通管理员不能禁用其他管理员（需超级管理员处理）。</div>

<h2 id="reset">6. 重置密码 <span class="tag">@UM-S18 ~ @UM-S21</span></h2>
<ol class="steps">
  <li>目标行点击「重置密码」。</li>
  <li>输入新密码并再次确认。规则：<b>8-20 位，必须同时包含字母和数字</b>（弹窗内有提示；两次不一致会在表单内直接指出，不会发起请求）。</li>
  <li>确认成功后，<b>请通过安全渠道（线下）将新密码告知用户</b>，建议对方登录后自行修改。</li>
</ol>
{fig('05-password-mismatch', '图 7 · 两次密码不一致时的就地校验提示')}
<div class="callout danger">不能重置超级管理员的密码；普通管理员不能重置其他管理员的密码。</div>

<h2 id="prefs">7. 主题与语言 <span class="tag">FE-TH / FE-SI</span></h2>
<ol class="steps">
  <li>侧边栏底部两个下拉框分别切换 亮/暗 主题 与 简体中文/English。</li>
  <li>选择即时生效并自动记住，下次打开保持不变。</li>
</ol>
{fig('09-dark-theme', '图 8 · 暗色主题下的用户管理页')}
{fig('10-english', '图 9 · 切换到英文界面（菜单显示 Users）')}

<h2 id="logout">8. 退出登录 <span class="tag">FE-AUTH</span></h2>
<ol class="steps">
  <li>点击左上角头像区域 → 「退出登录」。</li>
  <li>系统清空登录态并返回登录页；退出后直接访问受保护页面会被自动重定向回登录页。</li>
</ol>
{fig('11-logout', '图 10 · 退出后回到登录页')}

<h2 id="roles">9. 角色权限速查</h2>
<table>
  <tr><th>操作</th><th>超级管理员</th><th>管理员</th><th>普通用户</th></tr>
  <tr><td>用户列表 / 查看信息</td><td class="ok">✓</td><td class="ok">✓</td><td class="bad">✗（403）</td></tr>
  <tr><td>设为 / 取消管理员</td><td class="ok">✓</td><td class="bad">✗（403）</td><td class="bad">✗</td></tr>
  <tr><td>禁用 / 启用账号</td><td class="ok">✓（含管理员）</td><td class="ok">✓（仅普通用户）</td><td class="bad">✗</td></tr>
  <tr><td>重置密码</td><td class="ok">✓（除自己外）</td><td class="ok">✓（仅普通用户）</td><td class="bad">✗</td></tr>
  <tr><td>被保护的 target</td><td colspan="3">超级管理员账号不可被降级/禁用/重置；任何人不可操作自己；管理员之间互不可操作</td></tr>
</table>

<h2 id="faq">10. 常见问题</h2>
<table>
  <tr><th>现象</th><th>原因与处理</th></tr>
  <tr><td>授权后对方菜单没变化</td><td>前端资料未刷新——让对方刷新页面或重新登录即可</td></tr>
  <tr><td>禁用后对方还能操作几下</td><td>发生在禁用前已发出的请求；下一个请求必被拦截，无需处理</td></tr>
  <tr><td>点「设为管理员」报无权限</td><td>当前登录者不是超级管理员，请换 admin（root）操作</td></tr>
  <tr><td>重置密码报错</td><td>多为强度不足（8-20 位含字母+数字）或目标是受保护账号</td></tr>
  <tr><td>登录提示失败次数过多</td><td>连续错 5 次锁定 5 分钟，稍后再试或联系运维清理</td></tr>
</table>

<h2 id="ux">11. 交互合理性评估（2026-09-02 真实浏览器走查）</h2>
<h3 class="ok">✅ 合理的设计</h3>
<ul class="plain">
  <li><b>权限即所见：</b>root 行不渲染危险操作、普通管理员看不到授权按钮——界面不会诱导用户做无权操作。</li>
  <li><b>危险操作有后果说明：</b>禁用确认框写明「禁用后该用户将无法登录和使用系统」。</li>
  <li><b>密码规则前置提示：</b>重置密码弹窗直接展示 8-20 位字母+数字规则，两次不一致就地报错、不发请求。</li>
  <li><b>标准空态与总数：</b>搜索无结果提示「暂无数据」；分页器显示「共 N 条」。</li>
  <li><b>长内容防溢出：</b>邮箱列超长自动省略号截断。</li>
  <li><b>全局反馈统一：</b>所有成功/失败由统一反馈系统提示，不弹窗打断。</li>
</ul>
<h3 class="bad">⚠️ 已知不合理（历史遗留，已记录在文档，未擅自改动）</h3>
<ul class="plain">
  <li><b>未实现的菜单项（应用/知识库/团队/插件）可点击但无任何反馈</b>，点击后直接弹回概览页，用户会困惑（见 ui/docs/layout-routing 已知问题）。</li>
  <li>登录页第三方渠道未配置图标时会渲染空图片地址（控制台警告，属历史代码）。</li>
  <li>antd v5 + React 19 兼容警告为全局现状（作者选型，待上游升级）。</li>
</ul>
{fig('ux-05-ghost-menu', '图 11 · 点击「应用」菜单后无反馈直接回到概览（已知问题）')}

<footer>
  MoAI 用户管理操作手册 · 生成于 2026-09-02 · 配套文档：<code>docs/user-management/</code>（SDD/BDD/TDD/SOP 四件套）·
  验证证据：<code>local-dev/</code>（API 34 场景 + 浏览器 15 场景自动化脚本）
</footer>
</div></body></html>"""

out = os.path.join(ROOT, 'docs/user-management/manual.html')
with open(out, 'w', encoding='utf-8') as f:
    f.write(html)
print('written', out, f'{os.path.getsize(out)//1024} KB')
