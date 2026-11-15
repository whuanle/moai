# ==================== 前端构建阶段 ====================
# 前端编译为静态资源，最终由 .NET 后端从 wwwroot 托管（同源，无前后端分离部署）。
FROM node:22-slim AS frontend-builder

# 注：依赖均为纯 JS/预编译二进制（antd/vite/kiota/playwright），无 node-gyp 原生编译需求，
# 不需要 apt 安装 python3/make/g++（曾因 Docker VM 内 deb.debian.org DNS/502 反复构建失败）

WORKDIR /app

# 复制 package.json 和 package-lock.json
COPY ui/package*.json ./

# 按 lockfile 安装依赖。lockfile v3 已含各平台 @rollup/rollup-* 可选原生依赖，
# 无需删 lock 重装；旧写法 `rm -rf node_modules package-lock.json && npm install`
# 会在较新 npm 上触发 "Cannot read properties of null (reading 'edgesOut')" 而构建失败。
RUN npm ci

# 复制源代码
COPY ui/ .

# 构建应用（不注入 VITE_ServerUrl，生产走同源请求）
RUN npm run build

# ==================== 后端构建阶段 ====================
# SDK 钉 10.0.203 保证构建可复现。注：曾在 Apple Silicon 上用 QEMU 仿真 linux/amd64 构建时
# 遇到 10.0.302 restore 报 MSB4184 / 10.0.203 直接 SIGSEGV，均为仿真环境问题；
# 原生 arm64 与常规 amd64 CI 不受影响（原生 arm64 实测通过）
FROM mcr.microsoft.com/dotnet/sdk:10.0.203 AS backend-builder
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Packages.props", "."]
COPY ["Directory.Build.props", "."]
COPY ["src/", "src/"]
RUN dotnet restore "./src/MoAI/MoAI.csproj"
WORKDIR "/src/src/MoAI"
RUN dotnet build "./MoAI.csproj" -c $BUILD_CONFIGURATION -o /app/build
RUN dotnet publish "./MoAI.csproj" -c $BUILD_CONFIGURATION -o /app/publish

# ==================== 最终运行阶段 ====================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# aspnet:10.0 运行时缺少 Kerberos 库，启动会打印
# "Cannot load library libgssapi_krb5.so.2"；装上以消除该告警（不影响功能）。
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

# 创建配置和文件目录
RUN mkdir -p /app/configs /app/files

# 复制后端发布文件
COPY --from=backend-builder /app/publish .

# 后端自带静态资源（wwwroot/embed 悬浮对话组件等），显式复制避免依赖 publish 行为
COPY src/MoAI/wwwroot ./wwwroot

# 复制前端构建产物到 wwwroot（同源托管；与后端静态资源合并）
COPY --from=frontend-builder /app/dist ./wwwroot

# 内置配置模板（compose 默认值 + 对外地址占位符）：无挂载时由 entrypoint 复制为 /app/configs/system.json
COPY configs/system.example.json /app/configs/system.json.template

# 复制 entrypoint 脚本
COPY docker-entrypoint.sh /app/docker-entrypoint.sh
RUN chmod +x /app/docker-entrypoint.sh

# 配置注入链：MAI_FILE（默认 /app/configs/system.json）→ 后端配置加载器
ENV MAI_FILE=/app/configs/system.json

EXPOSE 8080

ENTRYPOINT ["/app/docker-entrypoint.sh"]
