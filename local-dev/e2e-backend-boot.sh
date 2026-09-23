#!/bin/zsh
# E2E 独立后端：system.local.json 拍平成 MoAI__ 环境变量（勿带 MAI_FILE——后加配置源会覆盖 env）
# 用法: ./local-dev/e2e-backend-boot.sh [port]  → 输出 PGID 供 kill -- -PGID 收尾
PORT="${1:-5211}"
cd "$(dirname "$0")/.." || exit 1
export ASPNETCORE_ENVIRONMENT=Development
export MoAI__Port="$PORT"
export MoAI__Server="http://127.0.0.1:$PORT"
export MoAI__WebUI="http://127.0.0.1:4000"
export MoAI__AES="moai_aes_key_2024"
export MoAI__DBType="postgres"
export MoAI__Database="Database=moai;Host=127.0.0.1;Password=moai123456;Port=5432;Username=postgres;Search Path=public"
export MoAI__Redis="127.0.0.1:55379"
export MoAI__RabbitMQ="amqp://guest:guest@127.0.0.1:55672"
export MoAI__Storage__Endpoint="http://127.0.0.1:9000"
export MoAI__Storage__ForcePathStyle=true
export MoAI__Storage__Bucket="moai"
export MoAI__Storage__AccessKeyId="moai"
export MoAI__Storage__AccessKeySecret="moai12345"
export MoAI__OTLP__Trace="http://127.0.0.1:4317"
export MoAI__OTLP__Metrics="http://127.0.0.1:4317"
export MoAI__OTLP__Protocol="0"
exec dotnet run --project src/MoAI/MoAI.csproj --no-build --no-restore
