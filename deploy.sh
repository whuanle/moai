#!/bin/bash

# MoAI Docker 部署脚本

set -e

echo "🚀 开始部署 MoAI 应用..."

# 检查 Docker 是否安装
if ! command -v docker &> /dev/null; then
    echo "❌ Docker 未安装，请先安装 Docker"
    exit 1
fi

# 检查 Docker Compose 是否安装
if ! command -v docker-compose &> /dev/null; then
    echo "❌ Docker Compose 未安装，请先安装 Docker Compose"
    exit 1
fi

# 检查配置文件
if [ ! -d "configs" ]; then
    echo "❌ configs 目录不存在，请确保配置文件已准备"
    exit 1
fi

# 检查环境变量文件
if [ ! -f ".env" ]; then
    echo "📝 创建 .env 文件..."
    cp env.example .env
    echo "✅ .env 文件已创建，请根据需要修改 BACKEND_URL"
fi

# 停止现有服务
echo "🛑 停止现有服务..."
docker-compose down 2>/dev/null || true

# 清理旧镜像（可选）
read -p "是否清理旧镜像？(y/N): " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
    echo "🧹 清理旧镜像..."
    docker system prune -f
fi

# 构建并启动服务
echo "🔨 构建并启动服务..."
docker-compose up -d --build

# 等待服务启动
echo "⏳ 等待服务启动..."
sleep 10

# 检查服务状态
echo "📊 检查服务状态..."
docker-compose ps

# 检查服务健康状态
echo "🏥 检查服务健康状态..."

# 检查后端服务
if curl -f http://localhost:5000/health 2>/dev/null; then
    echo "✅ 后端服务运行正常"
else
    echo "⚠️  后端服务可能还在启动中，请稍后检查"
fi

# 检查前端服务
if curl -f http://localhost:3000 2>/dev/null; then
    echo "✅ 前端服务运行正常"
else
    echo "⚠️  前端服务可能还在启动中，请稍后检查"
fi

echo ""
echo "🎉 部署完成！"
echo ""
echo "📱 访问地址："
echo "   前端: http://localhost:3000"
echo "   后端: http://localhost:5000"
echo ""
echo "📋 常用命令："
echo "   查看日志: docker-compose logs -f"
echo "   停止服务: docker-compose down"
echo "   重启服务: docker-compose restart"
echo ""
echo "🔧 如需修改配置，请编辑 .env 文件后重新部署" 