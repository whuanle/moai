# MoAI 宣传官网 Implementation Plan

> **For agentic workers:** 当前会话直接执行，不委派、不提交 Git。不修改管理后台或业务数据。

**Goal:** 制作独立、可直接打开的高端静态宣传官网，连接用户指定文档和 GitHub。

**Architecture:** 原生 HTML/CSS/JS 放在 website/；本地真实截图、字体与图标随页面交付。案例采用内置数据切换，不依赖接口、构建或服务器。

**Tech Stack:** HTML、CSS、JavaScript、本地图片、Lucide 静态图标、浏览器自动化。

---

## Task 1: 页面与资源

- [x] 核对文档、仓库及 Apache-2.0 许可证，选择已有真实产品素材。
- [x] 新建 website/index.html、styles.css、script.js；实现品牌首屏、能力、案例、架构、部署、FAQ 与开源入口。
- [x] 复制裁切后的真实产品截图、字体与许可；使用本地图标，无远程运行依赖。
- [x] node --check website/script.js；检查本地资源与锚点存在。

## Task 2: 交互与验收

- [x] 直接打开静态文件验证桌面、手机布局，截图至 website/qa/。
- [x] 验证案例切换、移动菜单、FAQ、部署命令复制和 Escape 关闭弹层。
- [x] 检查图片解码、控制台错误、页面溢出以及减少动态偏好。
- [x] 写 website/README.md，交付入口与已验证结果。

## Task 3: 宣传片与视觉升级

- [x] 将用户指定第四版 MP4 与封面复制到 website/assets，加入首屏观看入口与独立视频展示区。
- [x] 保留布局与结构，升级石墨黑、冷白、青色、荧光绿与珊瑚色；加入金属字标、流动网格、按钮扫光与能力悬停高亮。
- [x] 资源准备脚本同步打包视频、封面与播放图标，更新使用说明。
- [x] 四视口布局与原有交互回归通过；视频 SHA-256 一致，双视口播放、跳转、暂停及非空画面检查通过。

## 交付证据

- [入口](../../../website/index.html) / [使用说明](../../../website/README.md)。
- [浏览器验收](../../../website/qa/verification.json)：file:// 模式、四种视口、六组核心交互检查，全部 PASS。
- [桌面全页](../../../website/qa/full-1440.png) / [手机全页](../../../website/qa/full-390.png)：已视觉复查。
