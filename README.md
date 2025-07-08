

## 开发说明

在项目的 `configs` 目录提供各类配置模板，可参考该目录下的文件。

### 配置

项目支持环境变量和文件注入配置，建议统一 configs 目录统一管理配置文件。

创建环境变量，`MAI_CONFIG`，设置变量值为配置文件路径，配置文件支持 `.json`、`.yaml`、`.conf` 等类型。

如：

```
MAI_CONFIG = E:/configs/maiconfigs.json
```

![image-20250309210715585](images/image-20250309210715585.png)



使用 docker 启动时，可以通过 `docker -v /data/config:/app/configs ` 的形式向服务提供配置文件。
如果不单独配置 MAI_CONFIG，那么会自动使用 `configs/system.yaml` 作为默认配置文件。

```
-e MAI_CONFIG=/app/configs/system.json
```

### 指定端口
默认服务使用 8080 端口启动服务，如果需要改变，可以添加环境变量。

```
ASPNETCORE_HTTP_PORTS=80;8080
ASPNETCORE_HTTPS_PORTS=443;8081
```

或者

```
ASPNETCORE_URLS=http://*:80/;http://*:8080/;https://*:443/;https://*:8081/
```

### 配置
#### 服务端配置

服务端配置用于正确生成前端跳转地址、文件访问地址、swagger 访问。

```
"Server": "http://127.0.0.1:5000"
```

#### AES 

AES 加密配置用于加密敏感数据，如 ai key 等。

```
  "AES": "abcdef1234",
```

#### 数据库

数据库配置用于连接数据库，支持 MySQL、PostgreSQL、SQLite 等。

Mysql 示例:

```
  "DBType": "mysql",
  "Database": "Database=moai;Host=127.0.0.1;Password=aaa;Port=3306;Username=root",
```

#### Redis

```
  "Redis": "192.168.50.199:6379",
```

#### 向量化

向量化配置用于连接向量化服务，支持 postgres、 Pinecone、Weaviate、Milvus 等。
```
  "Wiki": {
    "DBType": "postgres",
    "Database": "Database=document;Host=192.168.50.199;Password=19971120;Port=5432;Username=postgres;Search Path=public"
  },
```

#### 消息传递

用于传递消息或后台处理任务，支持 RabbitMQ 和本地消息。

使用 RabbitMQ 时，需要安装 RabbitMQ 服务并配置连接信息。
```
  "Message": {
    "RabbitMQ": "amqp://guest:guest@127.0.0.1:5672"
    }
```

如果不使用 RabbitMQ，可以使用本地消息传递，配置留空即可。

```
  "Message": {
  }
```

#### 文件存储
支持 S3、MinIO、阿里云 OSS、腾讯云 COS 等文件存储服务，或者使用本地存储。

使用对象存储时，不支持自定义域名，只能使用原生存储桶名称。

```
  "Storage": {
    "Type": "S3",
      "Public": {
        "Endpoint": "https://cos.ap-guangzhou.myqcloud.com",
        "ForcePathStyle": false,
        "Bucket": "MoAI-00000",
        "AccessKeyId": "xxx",
        "AccessKeySecret": "xxx"
      },
      "Private": {
        "Endpoint": "https://cos.ap-guangzhou.myqcloud.com",
        "ForcePathStyle": false,
        "Bucket": "maomiprivate-00001",
        "AccessKeyId": "xxx",
        "AccessKeySecret": "xxx"
      }
    }
```

如果使用本地存储，则需要在 configs/system.yaml 中配置存储路径。
```
  "Storage": {
    "Type": "local",
    "FilePath": "E:\\configs\\maomi\\files",
  }
```

### 日志

你可以在 configs 目录下创建一个 logger.json 文件，MoAI 启动时会读取该文件作为日志配置，如果文件不存在则会自动创建一个默认的。

默认配置如下：

```
{
  "Serilog": {
    "Using": [
      "Serilog.Sinks.Console"
    ],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore.HttpLogging": "Information",
        "ProtoBuf.Grpc.Server.ServicesExtensions.CodeFirstServiceMethodProvider": "Warning",
        "Microsoft.EntityFrameworkCore": "Information",
        "Microsoft.AspNetCore": "Warning",
        "System.Net.Http.HttpClient.TenantManagerClient.LogicalHandler": "Warning",
        "Microsoft.EntityFrameworkCore.Database.Command.CommandExecuted": "Warning",
        "System": "Information",
        "Microsoft": "Information",
        "Grpc": "Information",
        "MySqlConnector": "Information"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "{SourceContext} {Scope} {Timestamp:HH:mm} [{Level}]{NewLine}{Properties:j}{NewLine}{Message:lj} {Exception} {NewLine}"
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
```
