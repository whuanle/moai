---
sidebar_position: 5
---



# 数据库插件

目前支持连接 Mysql、Postgres 数据库执行 SQL，插件不限制是 DDL 还是 DML，所以配置连接字符串是注意限制账户权限，以免 AI 执行的 SQL 把数据库搞坏了！



Postgres 数据库连接字符串配置模板：

```
Host=localhost;Port=5432;Username=myuser;Password=mypassword;Database=mydb;
```



Mysql 数据库连接字符串配置模板：

```
Host=localhost;Port=5432;Username=myuser;Password=mypassword;Database=mydb;
```

