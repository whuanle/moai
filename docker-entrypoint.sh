#!/bin/bash
set -e

# ============================================================
# MoAI 容器入口脚本
# 配置来源：MAI_FILE（默认 /app/configs/system.json）
#   - docker compose：bind-mount ./configs/system.json
#   - docker run    ：-v $(pwd)/configs/system.json:/app/configs/system.json:ro
# 未挂载时回退到镜像内置的 /app/configs/system.json.template
#
# 模板占位符（可选）：配置里可写 __MOAI_SERVER_URL__ / __S3_ENDPOINT__，
# 由本脚本按环境变量替换后生成运行时配置，避免手工改地址。
# ============================================================

mkdir -p /app/configs /app/files

MAI_FILE="${MAI_FILE:-/app/configs/system.json}"
export MAI_FILE

# 常见误用：宿主机缺少 system.json 时，docker 会在挂载点创建同名目录。
if [ -d "$MAI_FILE" ]; then
  echo "[entrypoint] ERROR: $MAI_FILE 是目录而不是文件。" >&2
  echo "[entrypoint] 请确认宿主机存在该配置文件后再启动，例如：- ./configs/system.json:/app/configs/system.json:ro" >&2
  exit 1
fi

if [ ! -f "$MAI_FILE" ]; then
  if [ -f /app/configs/system.json.template ]; then
    echo "[entrypoint] 未找到 $MAI_FILE，使用内置模板初始化。"
    cp /app/configs/system.json.template "$MAI_FILE"
  else
    echo "[entrypoint] ERROR: 找不到配置文件 $MAI_FILE" >&2
    exit 1
  fi
fi

# 占位符替换：__MOAI_SERVER_URL__=MoAI 对外地址，__S3_ENDPOINT__=对象存储对外地址
if grep -q -e '__MOAI_SERVER_URL__' -e '__S3_ENDPOINT__' "$MAI_FILE" 2>/dev/null; then
  MOAI_SERVER_URL="${MOAI_SERVER_URL:-http://localhost:8080}"
  S3_ENDPOINT="${S3_ENDPOINT:-http://localhost:9000}"
  RUNTIME_FILE="/app/configs/system.runtime.json"
  sed -e "s|__MOAI_SERVER_URL__|${MOAI_SERVER_URL}|g" \
      -e "s|__S3_ENDPOINT__|${S3_ENDPOINT}|g" \
      "$MAI_FILE" > "$RUNTIME_FILE"
  MAI_FILE="$RUNTIME_FILE"
  export MAI_FILE
  echo "[entrypoint] 已按 MOAI_SERVER_URL=${MOAI_SERVER_URL} 生成运行时配置 $RUNTIME_FILE"
fi

echo "[entrypoint] 使用配置文件: $MAI_FILE"
exec dotnet MoAI.dll
