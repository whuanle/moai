#!/bin/bash
set -e

# 生成配置文件
cat > /app/configs/system.json << EOF
{
  "MoAI": {
    "Port": 8080,
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
    },
    "OTLP": {
      "Trace": "${OTLP_TRACE:-}",
      "Metrics": "${OTLP_METRICS:-}",
      "Protocol": ${OTLP_PROTOCOL:-0}
    }
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
