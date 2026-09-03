# ==================== 前端构建阶段 ====================
FROM node:22-slim AS frontend-builder

# 注：依赖均为纯 JS/预编译二进制（antd/vite/kiota/playwright），无 node-gyp 原生编译需求，
# 不需要 apt 安装 python3/make/g++（曾因 Docker VM 内 deb.debian.org DNS/502 反复构建失败）

WORKDIR /app

# 复制 package.json 和 package-lock.json
COPY ui/package*.json ./

# 安装所有依赖
RUN npm ci

# 复制源代码
COPY ui/ .

# 重新安装依赖以解决 Rollup 可选依赖项问题
RUN rm -rf node_modules package-lock.json && npm install

# 构建应用
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

# 创建配置和文件目录
RUN mkdir -p /app/configs /app/files /app/wwwroot

# 复制后端发布文件
COPY --from=backend-builder /app/publish .

# 复制前端构建产物到 wwwroot
COPY --from=frontend-builder /app/dist ./wwwroot

# 复制 entrypoint 脚本
COPY docker-entrypoint.sh /app/docker-entrypoint.sh
RUN chmod +x /app/docker-entrypoint.sh

ENV MAI_FILE=/app/configs/system.json

ENTRYPOINT ["/app/docker-entrypoint.sh"]
