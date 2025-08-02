#!/bin/bash

# 获取后端地址环境变量，默认为 http://localhost:5000
BACKEND_URL=${BACKEND_URL:-http://localhost:5000}

echo "Configuring frontend to use backend at: $BACKEND_URL"

# 使用更精确的替换方法，替换构建时的占位符
# 查找所有 JavaScript 文件并替换占位符
find /usr/share/nginx/html -name "*.js" -type f -exec sed -i "s|__BACKEND_URL_PLACEHOLDER__|$BACKEND_URL|g" {} \;

# 也替换 HTML 文件中的占位符（如果有的话）
find /usr/share/nginx/html -name "*.html" -type f -exec sed -i "s|__BACKEND_URL_PLACEHOLDER__|$BACKEND_URL|g" {} \;

echo "Frontend configuration completed. Starting nginx..."

# 启动 nginx
exec nginx -g "daemon off;" 