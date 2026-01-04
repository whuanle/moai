# MoAI 样式设计规范

## 设计理念
- 大气、优雅、专业
- 柔和渐变背景营造层次感
- 精致阴影和圆角提升质感
- 统一间距和色彩系统

## CSS 变量引用
所有样式变量定义在 `src/styles/theme.css`，页面通过 `@import` 或直接使用变量。

## 核心变量速查

### 颜色
- `--color-primary`: #1677ff (主色)
- `--color-text-primary`: #1f1f1f (主文本)
- `--color-text-secondary`: #666666 (次要文本)
- `--color-bg-layout`: #f0f2f5 (布局背景)
- `--color-bg-container`: #ffffff (容器背景)
- `--color-border`: #e8e8e8 (边框)

### 阴影
- `--shadow-sm`: 轻微阴影，用于卡片
- `--shadow-md`: 中等阴影，用于悬浮效果
- `--shadow-lg`: 大阴影，用于弹窗

### 圆角
- `--radius-sm`: 6px (按钮、输入框)
- `--radius-md`: 8px (小卡片)
- `--radius-lg`: 12px (页面容器、大卡片)

### 间距
- `--spacing-sm`: 8px
- `--spacing-md`: 16px
- `--spacing-lg`: 24px
- `--spacing-xl`: 32px

### 布局
- `--header-height`: 60px
- `--content-max-width`: 1600px

## 页面容器类名

```css
/* 全宽白色背景容器 */
.page-container

/* 居中限宽容器 */
.page-container-centered

/* 透明背景容器 */
.page-container-transparent

```

## 通用组件类名

```css
/* 卡片 */
.moai-card              /* 基础卡片 */
.moai-card-interactive  /* 可交互卡片，hover 有上浮效果 */

/* 页面标题 */
.moai-page-header       /* 标题区域容器 */
.moai-page-title        /* 主标题 */
.moai-page-subtitle     /* 副标题 */

/* 工具栏 */
.moai-toolbar           /* 工具栏容器 */
.moai-toolbar-left      /* 左侧操作区 */
.moai-toolbar-right     /* 右侧操作区 */

/* 网格布局 */
.moai-grid              /* 基础网格 */
.moai-grid-2/3/4        /* 固定列数 */
.moai-grid-auto         /* 自适应列数 */

/* 状态 */
.moai-empty             /* 空状态 */
.moai-loading           /* 加载状态 */
```

## 页面模板示例

```tsx
function ExamplePage() {
  return (
    <div className="page-container">
      <div className="moai-page-header">
        <h1 className="moai-page-title">页面标题</h1>
        <p className="moai-page-subtitle">页面描述信息</p>
      </div>
      
      <div className="moai-toolbar">
        <div className="moai-toolbar-left">
          <Input.Search placeholder="搜索" />
        </div>
        <div className="moai-toolbar-right">
          <Button type="primary">新建</Button>
        </div>
      </div>
      
      <div className="moai-grid moai-grid-auto">
        {/* 卡片内容 */}
      </div>
    </div>
  );
}
```

## 注意事项
1. 优先使用 CSS 变量而非硬编码值
2. 卡片统一使用 `--radius-lg` 圆角
3. 页面内边距统一使用 `--spacing-lg`
4. 交互元素需添加 `transition` 过渡效果
5. 响应式断点：768px (移动端)、992px (平板)、1200px (桌面)
