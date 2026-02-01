#!/bin/bash
set -e

# 构建 OTLP 配置（如果设置了端点）
OTLP_CONFIG=""
if [ -n "$OTLP_TRACE_ENDPOINT" ] || [ -n "$OTLP_METRICS_ENDPOINT" ]; then
  OTLP_CONFIG=",
    \"OTLP\": {
      \"Trace\": \"${OTLP_TRACE_ENDPOINT:-}\",
      \"Metrics\": \"${OTLP_METRICS_ENDPOINT:-}\",
      \"Protocol\": ${OTLP_PROTOCOL:-0}
    }"
fi

# 生成配置文件
cat > /app/configs/system.json << EOF
{
  "MoAI": {
    "Server": "${MOAI_SERVER_URL:-http://localhost:8080}",
    "WebUI": "${MOAI_WEBUI_URL:-http://localhost:8080}",
    "AES": "${MOAI_AES_KEY:-moai_aes_key_2024}",
    "DBType": "postgres",
    "Database": "Database=${POSTGRES_DB:-moai};Host=${POSTGRES_HOST:-postgres};Password=${POSTGRES_PASSWORD:-moai123456};Port=${POSTGRES_PORT:-5432};Username=${POSTGRES_USER:-postgres};Search Path=public",
    "Redis": "${REDIS_HOST:-redis}:${REDIS_PORT:-6379}",
    "Wiki": {
      "DBType": "postgres",
      "Database": "Database=${POSTGRES_DB:-moai};Host=${POSTGRES_HOST:-postgres};Password=${POSTGRES_PASSWORD:-moai123456};Port=${POSTGRES_PORT:-5432};Username=${POSTGRES_USER:-postgres};Search Path=public"
    },
    "RabbitMQ": "amqp://${RABBITMQ_USER:-guest}:${RABBITMQ_PASSWORD:-guest}@${RABBITMQ_HOST:-rabbitmq}:${RABBITMQ_PORT:-5672}",
    "Storage": {
      "LocalPath": "/app/files"
    }${OTLP_CONFIG}
  },
  "Serilog": {
    "Using": [
      "Serilog.Sinks.Console"
    ],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore.HttpLogging": "Information",
        "Microsoft.EntityFrameworkCore": "Warning",
        "Microsoft.AspNetCore": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "{SourceContext} {Timestamp:HH:mm} [{Level}] {Message:lj} {Exception}{NewLine}"
        }
      }
    ],
    "Enrich": [
      "FromLogContext",
      "WithMachineName",
      "WithThreadId"
    ]
  }
}
EOF

echo "Configuration generated at /app/configs/system.json"

# 启动应用
exec dotnet MoAI.dll
