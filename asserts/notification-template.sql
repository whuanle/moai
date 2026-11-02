-- 内置通知模板种子数据（存量库补种：只插入缺失的 code，不覆盖已有模板；新库由 EnsureCreated 时 NotificationTemplateSeed.Apply 自动写入，无需执行本脚本）
-- 占位符为 {{变量}} 双花括号（与云邮件平台提审形态一致），渲染白名单见 NotificationTemplateRenderer
-- 新增内置模板时：先在 NotificationTemplateSeed 注册，再在本脚本同步补一条 insert
-- 用法：psql -h 127.0.0.1 -U postgres -d moai_v2 -f asserts/notification-template.sql
-- 前置：notification_template 表须已有 code 列（执行 asserts/notification.sql 或由后端启动幂等 DDL 补齐）
-- 说明：省略 id 列走 identity 自增，避免与存量行主键冲突；create_user_id=1 与新库种子（admin）一致

insert into notification_template (code, name, title, content, content_type, variables, aliyun_template_code, tencent_template_id, description, create_user_id, update_user_id)
select 'register_email_code', '注册验证码邮件', '{{siteName}} 注册验证码', $tpl$<div style="margin:0;padding:24px;background-color:#f5f6f8;font-family:'PingFang SC','Microsoft YaHei','Segoe UI',Arial,sans-serif;">
  <div style="max-width:520px;margin:0 auto;background-color:#ffffff;border:1px solid #e5e6eb;border-radius:12px;padding:32px;">
    <h2 style="margin:0 0 16px;font-size:18px;color:#1f2329;">{{siteName}} 验证码</h2>
    <p style="margin:0 0 24px;font-size:14px;line-height:22px;color:#4e5969;">你正在注册 {{siteName}}，请使用以下验证码完成注册，验证码 <b style="color:#1f2329;">{{expireMinutes}}</b> 分钟内有效。</p>
    <div style="padding:16px 24px;background-color:#f5f6f8;border-radius:8px;text-align:center;font-size:30px;font-weight:bold;letter-spacing:10px;color:#4f46e5;">{{code}}</div>
    <p style="margin:24px 0 0;font-size:12px;line-height:20px;color:#86909c;">如非本人操作，请忽略本邮件；请勿将验证码透露给他人。</p>
  </div>
</div>$tpl$, 1, '[{"name":"code","description":"验证码","example":"123456"},{"name":"siteName","description":"网站名称","example":"MoAI"},{"name":"expireMinutes","description":"验证码有效分钟数","example":"10"}]', '', '', '平台内置模板：注册验证码邮件，系统设置未绑定自定义模板时自动使用；可编辑内容，不可删除。', 1, 1
where not exists (select 1 from notification_template where code = 'register_email_code' and is_deleted = 0);

insert into notification_template (code, name, title, content, content_type, variables, aliyun_template_code, tencent_template_id, description, create_user_id, update_user_id)
select 'login_email_code', '登录验证码邮件', '{{siteName}} 登录验证码', $tpl$<div style="margin:0;padding:24px;background-color:#f5f6f8;font-family:'PingFang SC','Microsoft YaHei','Segoe UI',Arial,sans-serif;">
  <div style="max-width:520px;margin:0 auto;background-color:#ffffff;border:1px solid #e5e6eb;border-radius:12px;padding:32px;">
    <h2 style="margin:0 0 16px;font-size:18px;color:#1f2329;">{{siteName}} 验证码</h2>
    <p style="margin:0 0 24px;font-size:14px;line-height:22px;color:#4e5969;">你正在登录 {{siteName}}，请使用以下验证码完成登录，验证码 <b style="color:#1f2329;">{{expireMinutes}}</b> 分钟内有效。</p>
    <div style="padding:16px 24px;background-color:#f5f6f8;border-radius:8px;text-align:center;font-size:30px;font-weight:bold;letter-spacing:10px;color:#4f46e5;">{{code}}</div>
    <p style="margin:24px 0 0;font-size:12px;line-height:20px;color:#86909c;">如非本人操作，请忽略本邮件；请勿将验证码透露给他人。</p>
  </div>
</div>$tpl$, 1, '[{"name":"code","description":"验证码","example":"123456"},{"name":"siteName","description":"网站名称","example":"MoAI"},{"name":"expireMinutes","description":"验证码有效分钟数","example":"10"}]', '', '', '平台内置模板：登录验证码邮件，系统设置未绑定自定义模板时自动使用；可编辑内容，不可删除。', 1, 1
where not exists (select 1 from notification_template where code = 'login_email_code' and is_deleted = 0);

insert into notification_template (code, name, title, content, content_type, variables, aliyun_template_code, tencent_template_id, description, create_user_id, update_user_id)
select 'reset_email_code', '找回密码验证码邮件', '{{siteName}} 找回密码验证码', $tpl$<div style="margin:0;padding:24px;background-color:#f5f6f8;font-family:'PingFang SC','Microsoft YaHei','Segoe UI',Arial,sans-serif;">
  <div style="max-width:520px;margin:0 auto;background-color:#ffffff;border:1px solid #e5e6eb;border-radius:12px;padding:32px;">
    <h2 style="margin:0 0 16px;font-size:18px;color:#1f2329;">{{siteName}} 验证码</h2>
    <p style="margin:0 0 24px;font-size:14px;line-height:22px;color:#4e5969;">你正在找回 {{siteName}} 账号密码，请使用以下验证码完成找回，验证码 <b style="color:#1f2329;">{{expireMinutes}}</b> 分钟内有效。</p>
    <div style="padding:16px 24px;background-color:#f5f6f8;border-radius:8px;text-align:center;font-size:30px;font-weight:bold;letter-spacing:10px;color:#4f46e5;">{{code}}</div>
    <p style="margin:24px 0 0;font-size:12px;line-height:20px;color:#86909c;">如非本人操作，请忽略本邮件；请勿将验证码透露给他人。</p>
  </div>
</div>$tpl$, 1, '[{"name":"code","description":"验证码","example":"123456"},{"name":"siteName","description":"网站名称","example":"MoAI"},{"name":"expireMinutes","description":"验证码有效分钟数","example":"10"}]', '', '', '平台内置模板：找回密码验证码邮件，系统设置未绑定自定义模板时自动使用；可编辑内容，不可删除。', 1, 1
where not exists (select 1 from notification_template where code = 'reset_email_code' and is_deleted = 0);
