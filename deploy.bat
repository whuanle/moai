@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

echo 🚀 开始部署 MoAI 应用...

REM 检查 Docker 是否安装
docker --version >nul 2>&1
if errorlevel 1 (
    echo ❌ Docker 未安装，请先安装 Docker
    pause
    exit /b 1
)

REM 检查 Docker Compose 是否安装
docker-compose --version >nul 2>&1
if errorlevel 1 (
    echo ❌ Docker Compose 未安装，请先安装 Docker Compose
    pause
    exit /b 1
)

REM 检查配置文件
if not exist "configs" (
    echo ❌ configs 目录不存在，请确保配置文件已准备
    pause
    exit /b 1
)

REM 检查环境变量文件
if not exist ".env" (
    echo 📝 创建 .env 文件...
    copy env.example .env >nul
    echo ✅ .env 文件已创建，请根据需要修改 BACKEND_URL
)

REM 停止现有服务
echo 🛑 停止现有服务...
docker-compose down >nul 2>&1

REM 询问是否清理旧镜像
set /p cleanup="是否清理旧镜像？(y/N): "
if /i "!cleanup!"=="y" (
    echo 🧹 清理旧镜像...
    docker system prune -f
)

REM 构建并启动服务
echo 🔨 构建并启动服务...
docker-compose up -d --build

REM 等待服务启动
echo ⏳ 等待服务启动...
timeout /t 10 /nobreak >nul

REM 检查服务状态
echo 📊 检查服务状态...
docker-compose ps

echo.
echo 🎉 部署完成！
echo.
echo 📱 访问地址：
echo    前端: http://localhost:3000
echo    后端: http://localhost:5000
echo.
echo 📋 常用命令：
echo    查看日志: docker-compose logs -f
echo    停止服务: docker-compose down
echo    重启服务: docker-compose restart
echo.
echo 🔧 如需修改配置，请编辑 .env 文件后重新部署
echo.
pause 