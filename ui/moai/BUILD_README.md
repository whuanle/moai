# 构建说明

本项目提供了多种构建选项来处理不同的开发和生产需求。

## 构建脚本

### 1. 开发构建（推荐）
```bash
npm run build
```
- 使用宽松的 TypeScript 检查
- 允许未使用的变量和参数
- 适合日常开发和快速构建

### 2. 严格构建
```bash
npm run build:strict
```
- 使用更严格的 TypeScript 检查
- 不检查未使用的变量和参数（已禁用）
- 适合生产环境构建

### 3. 代码检查
```bash
# 检查代码
npm run lint

# 自动修复可修复的问题
npm run lint:fix
```

## TypeScript 配置

### 开发配置 (`tsconfig.app.json`)
- `noUnusedLocals: false` - 允许未使用的局部变量
- `noUnusedParameters: false` - 允许未使用的参数
- 适合开发阶段使用

### 构建配置 (`tsconfig.build.json`)
- `noUnusedLocals: false` - 不检查未使用的局部变量
- `noUnusedParameters: false` - 不检查未使用的参数
- `allowImportingTsExtensions: true` - 支持 TypeScript 扩展导入
- `noEmit: true` - 不生成输出文件（由 Vite 处理）
- 适合生产构建使用

## 常见问题解决

### 0. TypeScript 配置错误
如果遇到 `allowImportingTsExtensions` 相关错误：
- 确保 `noEmit: true` 或 `emitDeclarationOnly: true`
- 检查导入语句是否使用了 `.ts` 或 `.tsx` 扩展名
- 如果不需要扩展名导入，可以设置 `allowImportingTsExtensions: false`

### 1. 未使用的导入错误
如果遇到未使用的导入错误，可以：

1. **删除未使用的导入**（推荐）
2. **使用宽松构建**：`npm run build`
3. **添加 ESLint 忽略注释**：
   ```typescript
   // eslint-disable-next-line @typescript-eslint/no-unused-vars
   import { unusedComponent } from './components';
   ```

### 2. 未使用的参数错误
如果遇到未使用的参数错误，可以：

1. **删除未使用的参数**
2. **使用下划线前缀**：
   ```typescript
   function handleClick(_event: React.MouseEvent) {
     // 使用下划线前缀表示故意不使用的参数
   }
   ```
3. **使用宽松构建**：`npm run build`

### 3. 开发 vs 生产构建

**开发阶段**：
- 使用 `npm run build` 进行快速构建
- 允许未使用的代码存在
- 专注于功能开发

**生产阶段**：
- 使用 `npm run build:strict` 进行构建
- 确保代码质量
- 不强制要求移除未使用的代码

## Docker 构建

Docker 构建默认使用宽松的构建配置，确保构建成功。如果需要严格检查，可以修改 Dockerfile：

```dockerfile
# 使用严格构建
RUN npm run build:strict
```

## 最佳实践

1. **开发时**：使用宽松构建，专注于功能实现
2. **提交前**：运行 `npm run lint` 检查代码质量
3. **生产构建**：使用严格构建，确保代码质量
4. **定期清理**：删除未使用的导入和变量 