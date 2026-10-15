# MoAI 宣传片截图修订 Implementation Plan

> **For agentic workers:** 按本计划逐项执行，不委派子代理；仅修改制作目录，不提交 Git、不保存业务应用配置。

**Goal:** 删除经营分析与用户自助案例，使用用户指定流程和应用的真实配置截图，补足机票知识库与知识图谱证据。

**Architecture:** 保留第三版，在 outputs/promo-runs/2026-10-13-moai-v4 内修订 film.json 和既有生成器。用浏览器采集真实页面，重建连续时间线、旁白、合成与 MP4，继续使用 GitHub 片尾。

**Tech Stack:** HyperFrames、GSAP、Sharp、Windows Speech、FFmpeg、浏览器截图。

---

### Task 1: 真实素材

**Files:** 创建第四版 assets/ui 下的 workflow-live.png、flight-setup-live.png、flight-knowledge-live.png、flight-graph-live.png、logistics-setup-live.png、ops-setup-live.png；更新 prepare-assets.mjs 的素材清单。

- [ ] 用已有登录会话读取流程设计与三个应用页面，记录页面标题及绑定资源。
- [ ] 截取画布或配置主体，排除导航和无关个人信息；不保存、发布或执行应用。
- [ ] 检查截图非空且内容对应指定应用，机票知识库与知识图谱各有真实证据。

### Task 2: 叙事修订

**Files:** 修改第四版 film.json、build-composition.mjs、compose-audio.mjs、prepare-assets.mjs、verify-delivery.mjs。

- [ ] 删除 sales、integration 两镜头，保留其余镜头原时长，顺次重排到 118 秒。
- [ ] 流程能力与开场引用 workflow-live.png；三个案例搭建画面引用对应真实配置。
- [ ] 机票搭建阶段分时展示应用配置、知识库与图谱；字幕和旁白保留三方关系。
- [ ] 根据新时间线计算字幕、CTA、音乐安全段、案例数与镜头总数，避免残留旧硬编码。
- [ ] 执行 npm run assets、./generate-voice.ps1、npm run audio、npm run stage、npm run lint。
- [ ] 预期：118 秒、13 镜头、6 案例、24 阶段，旧案例和通用机票/物流搭建截图不再引用。

### Task 3: 导出与交付

**Files:** 创建第四版 final/promo.mp4、qa/verification.json；更新 05-delivery.md、06-music-plan.md。

- [ ] 对流程、机票搭建三张图、物流与运维搭建、GitHub 片尾进行合成关键帧检查。
- [ ] 使用现有 Headless Shell，运行 hyperframes render render-project --fps 30 --quality high --gpu --workers 3 --strict --no-best-effort --output final/promo.mp4。
- [ ] 执行 npm run verify；预期完整解码、单层黑字字幕、音量和关键帧检查通过。
- [ ] 启动本机预览，验证播放、跳转、桌面和手机布局，提供成片与来源记录。
