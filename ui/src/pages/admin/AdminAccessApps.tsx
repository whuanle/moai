import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  CheckCircleOutlined,
  CopyOutlined,
  DeleteOutlined,
  EditOutlined,
  EyeOutlined,
  KeyOutlined,
  PlusOutlined,
  SearchOutlined,
  StopOutlined,
} from '@ant-design/icons';
import { Alert, Button, Form, Input, Modal, Popconfirm, Radio, Space, Tag, Tooltip, Typography } from 'antd';
import type { TableColumnsType } from 'antd';
import { useTranslation } from 'react-i18next';
import { AuditCell, DataTable, PageToolbar, feedback } from '@/design-system';
import { controlHeight, spacing, tableCardChromeHeight } from '@/design-system/theme';
import {
  GrantScopeEditor,
  type GrantScopeOrgGrant,
  type GrantScopeRoleNode,
  type GrantScopeTeamOption,
  type GrantScopeValue,
} from '@/components/GrantScopeEditor';
import { getAdminTeams } from '@/api/team';
import { getOrgRoles, type OrgRoleItem } from '@/api/org';
import {
  createSubsysApp,
  deleteSubsysApp,
  getSubsystemAuthorizedTeams,
  getSubsystemOrgGrants,
  getSubsysApps,
  resetSubsysAppSecret,
  revealSubsysAppSecret,
  setSubsystemOrgGrants,
  updateSubsysApp,
  type AccessAppTypeValue,
  type OrgGrantScopeType,
  type SubsysAppItem,
  type SubsystemTeamScopeType,
} from '@/api/subsystem';

const { Text, Paragraph } = Typography;

const pageVerticalPadding = spacing.lg * 2;
const toolbarHeight = controlHeight + spacing.md;
const tableHeaderHeight = 55;
const tablePaginationHeight = 57;
// 高度预算仅作 scroll.y 兜底：布局高度由 AdminIntegrations.css 的 flex 链分配，
// 表体内部滚动、分页固定在卡片底部（.admin-integrations-fill 覆盖内联 max-height）
const accessAppsTableScrollY = `calc(100vh - ${pageVerticalPadding + toolbarHeight + tableHeaderHeight + tablePaginationHeight + tableCardChromeHeight}px)`;

interface AccessAppFormValues {
  appType: AccessAppTypeValue;
  key?: string;
  name: string;
  description?: string;
  scope?: GrantScopeValue;
  callbackUrls?: string[];
}

/** 组织平铺列表（按 path 层级序）组树 */
function buildRoleTree(roles: OrgRoleItem[]): GrantScopeRoleNode[] {
  const nodes = new Map<number, GrantScopeRoleNode>();
  for (const role of roles) {
    nodes.set(Number(role.orgRoleId), {
      title: role.name || `#${role.orgRoleId}`,
      value: Number(role.orgRoleId),
    });
  }
  const roots: GrantScopeRoleNode[] = [];
  for (const role of roles) {
    const node = nodes.get(Number(role.orgRoleId))!;
    const parentId = role.parentId == null ? null : Number(role.parentId);
    const parent = parentId == null ? null : nodes.get(parentId);
    if (parent) {
      parent.children = [...(parent.children ?? []), node];
    } else {
      roots.push(node);
    }
  }
  return roots;
}

/** 表单授权范围初值（全员可用） */
function defaultScope(): GrantScopeValue {
  return { allUsers: true, teamIds: [], orgGrants: [] };
}

/**
 * 系统级接入面板（仅 root）：子系统与应用接入同一列表、同一套管理接口。
 * 子系统=MoAI 生态独立部署服务（appid 自填 + 委托 SSO 回调 + 团队/组织授权 + 心跳在线）；
 * 应用接入=交给外部系统（ERP/OA 等）的凭证（appid 自动生成 moai-sa-，全量访问协作面资源）。
 * 密钥原文仅在创建/重置时显示一次。
 */
export function AccessAppsPanel() {
  const { t } = useTranslation();

  const [loading, setLoading] = useState(true);
  const [items, setItems] = useState<SubsysAppItem[]>([]);
  const [keyword, setKeyword] = useState('');

  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<SubsysAppItem | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [teamOptions, setTeamOptions] = useState<GrantScopeTeamOption[]>([]);
  const [teamLoading, setTeamLoading] = useState(false);
  const [roleTree, setRoleTree] = useState<GrantScopeRoleNode[]>([]);
  const [revealTarget, setRevealTarget] = useState<SubsysAppItem | null>(null);
  const [revealing, setRevealing] = useState(false);
  const [revealedSecret, setRevealedSecret] = useState<string | null>(null);
  const [form] = Form.useForm<AccessAppFormValues>();

  const formAppType = Form.useWatch('appType', form);
  const isSubsystemForm = formAppType !== 'app';

  const load = useCallback(
    async (search = keyword) => {
      setLoading(true);
      try {
        const list = await getSubsysApps(search);
        setItems(list);
      } catch {
        // 错误已由全局请求中间件统一提示
      } finally {
        setLoading(false);
      }
    },
    [keyword],
  );

  const loadTeams = useCallback(async () => {
    setTeamLoading(true);
    try {
      // 尽量一次拉全团队，避免选中标签因选项缺失回退显示 id
      const res = await getAdminTeams({ pageNo: 1, pageSize: 1000 });
      setTeamOptions(
        (res.items ?? []).map((team) => ({
          label: team.name || String(team.teamId ?? ''),
          value: Number(team.teamId),
          avatar: team.avatar ?? null,
        })),
      );
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setTeamLoading(false);
    }
  }, []);

  const loadRoleTree = useCallback(async () => {
    try {
      const roles = await getOrgRoles();
      setRoleTree(buildRoleTree(roles));
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  }, []);

  useEffect(() => {
    void load('');
    void loadTeams();
    void loadRoleTree();
    // 首次进入拉取，后续由交互触发
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleSearch = () => {
    void load(keyword);
  };

  /** 信封解密回显密钥原文（仅点击查看/复制时临时请求，刷新列表只见掩码） */
  const revealById = useCallback(async (id: string) => {
    setRevealing(true);
    try {
      const secret = await revealSubsysAppSecret(id);
      setRevealedSecret(secret);
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setRevealing(false);
    }
  }, []);

  const openReveal = useCallback((record: SubsysAppItem, autoReveal = false) => {
    setRevealTarget(record);
    setRevealedSecret(null);
    if (autoReveal && record.id) {
      void revealById(record.id);
    }
  }, [revealById]);

  const openCreate = () => {
    setEditing(null);
    form.resetFields();
    form.setFieldsValue({ appType: 'subsystem', callbackUrls: [], scope: defaultScope() });
    setModalOpen(true);
  };

  const openEdit = async (record: SubsysAppItem) => {
    setEditing(record);
    const isSubsystem = record.appType !== 'app';
    form.setFieldsValue({
      appType: isSubsystem ? 'subsystem' : 'app',
      key: record.key ?? '',
      name: record.name ?? '',
      description: record.description ?? '',
      scope: { allUsers: record.teamScope !== 'specifiedTeams', teamIds: [], orgGrants: [] },
      callbackUrls: record.callbackUrls?.length ? record.callbackUrls : [],
    });
    setModalOpen(true);
    if (!isSubsystem || !record.id || record.teamScope !== 'specifiedTeams') return;
    // 指定范围时回显：团队取 id 列表，组织取角色+方向行
    try {
      const teamIds = await getSubsystemAuthorizedTeams(record.id);
      form.setFieldValue('scope', {
        ...(form.getFieldValue('scope') as GrantScopeValue),
        teamIds,
      });
    } catch {
      // 错误已由全局请求中间件统一提示
    }
    try {
      const grants = await getSubsystemOrgGrants(record.id);
      form.setFieldValue('scope', {
        ...(form.getFieldValue('scope') as GrantScopeValue),
        orgGrants: grants.map(
          (grant): GrantScopeOrgGrant => ({
            orgRoleId: Number(grant.orgRoleId),
            scope: (grant.scope ?? 'self') as GrantScopeOrgGrant['scope'],
          }),
        ),
      });
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  };

  const handleSubmit = async () => {
    const values = await form.validateFields();
    const isSubsystem = values.appType !== 'app';
    setSubmitting(true);
    try {
      if (editing?.id) {
        const scope = values.scope ?? defaultScope();
        const teamScope: SubsystemTeamScopeType = scope.allUsers ? 'allTeams' : 'specifiedTeams';
        const teamIds = scope.allUsers ? [] : (scope.teamIds ?? []);
        const orgGrants = scope.allUsers ? [] : (scope.orgGrants ?? []).filter((row) => row.orgRoleId > 0);
        await updateSubsysApp(editing.id, {
          name: values.name,
          description: values.description,
          status: editing.status === 'disabled' ? 'disabled' : 'enabled',
          // 应用接入行后端忽略子系统专属字段，仅子系统行携带
          teamScope: isSubsystem ? teamScope : undefined,
          teamIds: isSubsystem ? teamIds : undefined,
          callbackUrls: isSubsystem ? values.callbackUrls ?? [] : undefined,
        });
        if (isSubsystem) {
          await setSubsystemOrgGrants(editing.id, orgGrants);
        }
        feedback.success(t('adminAccessApps.updateSuccess'));
        setModalOpen(false);
      } else {
        const scope = values.scope ?? defaultScope();
        const teamScope: SubsystemTeamScopeType = scope.allUsers ? 'allTeams' : 'specifiedTeams';
        const teamIds = scope.allUsers ? [] : (scope.teamIds ?? []);
        const orgGrants = scope.allUsers ? [] : (scope.orgGrants ?? []).filter((row) => row.orgRoleId > 0);
        const created = await createSubsysApp({
          appType: values.appType,
          key: values.key,
          name: values.name,
          description: values.description,
          teamScope,
          teamIds,
          callbackUrls: values.callbackUrls ?? [],
        });
        if (created.id && isSubsystem) {
          await setSubsystemOrgGrants(created.id, orgGrants);
        }
        feedback.success(t('adminAccessApps.createSuccess'));
        setModalOpen(false);
        // 创建成功自动信封解密展示一次原文（应用接入同时展示自动生成的 appid；此后可随时再查看/复制）
        if (created.id) {
          openReveal({ id: created.id, key: created.key, secretMask: created.secretMask }, true);
        }
      }
      void load();
    } catch {
      // 错误已由全局请求中间件统一提示
    } finally {
      setSubmitting(false);
    }
  };

  const handleToggleStatus = async (record: SubsysAppItem) => {
    if (!record.id) return;
    const isSubsystem = record.appType !== 'app';
    try {
      const teamIds =
        isSubsystem && record.teamScope === 'specifiedTeams' ? await getSubsystemAuthorizedTeams(record.id) : [];
      await updateSubsysApp(record.id, {
        name: record.name ?? '',
        description: record.description ?? '',
        status: record.status === 'enabled' ? 'disabled' : 'enabled',
        teamScope: isSubsystem ? record.teamScope ?? 'allTeams' : undefined,
        teamIds: isSubsystem ? teamIds : undefined,
        callbackUrls: isSubsystem ? record.callbackUrls ?? [] : undefined,
      });
      feedback.success(
        t(
          record.status === 'enabled'
            ? 'adminAccessApps.disableSuccess'
            : 'adminAccessApps.enableSuccess',
        ),
      );
      void load();
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  };

  const handleResetSecret = async (record: SubsysAppItem) => {
    if (!record.id) return;
    try {
      await resetSubsysAppSecret(record.id);
      feedback.success(t('adminAccessApps.resetSecretSuccess'));
      // 重置成功自动信封解密展示一次新原文
      openReveal(record, true);
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  };

  const handleDelete = async (record: SubsysAppItem) => {
    if (!record.id) return;
    try {
      await deleteSubsysApp(record.id);
      feedback.success(t('adminAccessApps.deleteSuccess'));
      void load();
    } catch {
      // 错误已由全局请求中间件统一提示
    }
  };

  const copyText = async (value: string) => {
    try {
      await navigator.clipboard.writeText(value);
      feedback.success(t('common.copySuccess'));
    } catch {
      feedback.error(t('common.copyFailed'));
    }
  };

  const columns: TableColumnsType<SubsysAppItem> = useMemo(
    () => [
      {
        title: t('adminAccessApps.colName'),
        dataIndex: 'name',
        width: 180,
        render: (value: string | null, record) => (
          <div style={{ lineHeight: 1.4, minWidth: 0 }}>
            <div>{value || '-'}</div>
            {record.description?.trim() && (
              <Text type="secondary" style={{ fontSize: 12 }}>
                {record.description}
              </Text>
            )}
          </div>
        ),
      },
      {
        title: t('adminAccessApps.colType'),
        dataIndex: 'appType',
        key: 'appType',
        width: 100,
        render: (value: AccessAppTypeValue | null) =>
          value === 'app' ? (
            <Tag color="purple">{t('adminAccessApps.typeApp')}</Tag>
          ) : (
            <Tag color="geekblue">{t('adminAccessApps.typeSubsystem')}</Tag>
          ),
      },
      {
        title: t('adminAccessApps.colKey'),
        key: 'key',
        width: 200,
        render: (_, record) => {
          const isSubsystem = record.appType !== 'app';
          const display = isSubsystem ? record.key : record.keyPrefix;
          return (
            <div style={{ lineHeight: 1.4, minWidth: 0 }}>
              <Space size={0}>
                <Text code>{display || '-'}</Text>
                <Tooltip title={t('common.copy')}>
                  <Button
                    type="text"
                    size="small"
                    icon={<CopyOutlined />}
                    aria-label={t('common.copy')}
                    onClick={() => void copyText(display ?? '')}
                  />
                </Tooltip>
              </Space>
              {/* 密钥掩码副行：便于核对，原文点击「查看密钥」信封解密获取 */}
              {record.secretMask?.trim() && (
                <div>
                  <Text type="secondary" code style={{ fontSize: 12 }}>
                    {t('adminAccessApps.secretMaskLabel')} {record.secretMask}
                  </Text>
                </div>
              )}
            </div>
          );
        },
      },
      {
        title: t('adminAccessApps.colScope'),
        key: 'scope',
        width: 280,
        render: (_, record) => {
          // 应用接入即开即用，全量访问协作面资源，无授权配置
          if (record.appType === 'app') {
            return <Text type="secondary">-</Text>;
          }
          // 授权范围 = 团队维度 + 组织维度，任一命中即可使用（SSO 门禁）
          const orgScopes: Record<OrgGrantScopeType, string> = {
            self: '',
            up: t('adminAccessApps.orgScopeUpShort'),
            down: t('adminAccessApps.orgScopeDownShort'),
          };
          const teamTags =
            record.teamScope === 'specifiedTeams'
              ? (record.authorizedTeams ?? []).map((team) => (
                  <Tag key={team.teamId ?? team.teamName}>
                    {team.teamName || `#${team.teamId}`}
                  </Tag>
                ))
              : [
                  <Tag key="all" color="geekblue">
                    {t('adminAccessApps.scopeAllTeams')}
                  </Tag>,
                ];
          const orgTags = (record.authorizedOrgs ?? []).map((org) => (
            <Tag key={`org-${org.orgRoleId}`} color="purple">
              {`${org.orgName || `#${org.orgRoleId}`}${orgScopes[org.scope ?? 'self']}`}
            </Tag>
          ));
          if (teamTags.length === 0 && orgTags.length === 0) {
            return (
              <Tag>
                {t('adminAccessApps.scopeSpecifiedTeams', { count: 0 })}
              </Tag>
            );
          }
          return (
            <Space size={4} wrap>
              {teamTags}
              {orgTags}
            </Space>
          );
        },
      },
      {
        title: t('adminAccessApps.colStatus'),
        key: 'status',
        width: 90,
        render: (_, record) =>
          record.status === 'disabled' ? (
            <Tag color="error">{t('adminAccessApps.statusDisabled')}</Tag>
          ) : (
            <Tag color="success">{t('adminAccessApps.statusEnabled')}</Tag>
          ),
      },
      {
        title: t('adminAccessApps.colOnline'),
        key: 'online',
        width: 120,
        render: (_, record) => {
          // 在线为子系统差异能力（应用接入未实现心跳时显示 -）
          if (record.appType === 'app') {
            return <Text type="secondary">-</Text>;
          }
          if (!record.online) {
            return <Tag>{t('adminAccessApps.offline')}</Tag>;
          }
          const count = record.onlineInstances ?? 0;
          return (
            <Tag color="processing">
              {count > 1
                ? t('adminAccessApps.onlineWithInstances', { count })
                : t('adminAccessApps.online')}
            </Tag>
          );
        },
      },
      {
        title: t('adminAccessApps.colCreate'),
        key: 'create',
        width: 150,
        render: (_, record) => (
          <AuditCell user={record.createUserName} time={record.createTime} />
        ),
      },
      {
        title: t('adminAccessApps.colUpdate'),
        key: 'update',
        width: 150,
        render: (_, record) => (
          <AuditCell user={record.updateUserName} time={record.updateTime} />
        ),
      },
      {
        title: t('adminAccessApps.colActions'),
        key: 'actions',
        width: 380,
        fixed: 'right',
        render: (_, record) => (
          <Space size={0}>
            <Button
              type="link"
              size="small"
              icon={<EditOutlined />}
              onClick={() => void openEdit(record)}
            >
              {t('adminAccessApps.edit')}
            </Button>
            <Popconfirm
              title={t(
                record.status === 'enabled'
                  ? 'adminAccessApps.disableConfirm'
                  : 'adminAccessApps.enableConfirm',
              )}
              okButtonProps={{ danger: record.status === 'enabled' }}
              onConfirm={() => void handleToggleStatus(record)}
            >
              <Button
                type="link"
                size="small"
                danger={record.status === 'enabled'}
                icon={
                  record.status === 'enabled' ? (
                    <StopOutlined />
                  ) : (
                    <CheckCircleOutlined />
                  )
                }
              >
                {t(
                  record.status === 'enabled'
                    ? 'adminAccessApps.disable'
                    : 'adminAccessApps.enable',
                )}
              </Button>
            </Popconfirm>
            <Popconfirm
              title={t('adminAccessApps.resetSecretConfirm')}
              onConfirm={() => void handleResetSecret(record)}
            >
              <Button type="link" size="small" icon={<KeyOutlined />}>
                {t('adminAccessApps.resetSecret')}
              </Button>
            </Popconfirm>
            <Button
              type="link"
              size="small"
              icon={<EyeOutlined />}
              onClick={() => openReveal(record)}
            >
              {t('adminAccessApps.viewSecret')}
            </Button>
            <Popconfirm
              title={t('adminAccessApps.deleteConfirm')}
              okButtonProps={{ danger: true }}
              onConfirm={() => void handleDelete(record)}
            >
              <Button type="link" size="small" danger icon={<DeleteOutlined />}>
                {t('adminAccessApps.delete')}
              </Button>
            </Popconfirm>
          </Space>
        ),
      },
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t, keyword],
  );

  return (
    <>
      <div
        style={{
          display: 'flex',
          flexDirection: 'column',
          height: '100%',
          minHeight: 0,
        }}
      >
        <PageToolbar
          filters={
            <Space size={spacing.xs} wrap>
              <Input
                value={keyword}
                onChange={(e) => setKeyword(e.target.value)}
                onPressEnter={() => handleSearch()}
                allowClear
                maxLength={50}
                prefix={<SearchOutlined style={{ color: 'inherit' }} />}
                placeholder={t('adminAccessApps.searchPlaceholder')}
                style={{ width: 260 }}
              />
              <Button
                type="primary"
                icon={<SearchOutlined />}
                onClick={() => handleSearch()}
              >
                {t('adminAccessApps.search')}
              </Button>
            </Space>
          }
          actions={
            <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>
              {t('adminAccessApps.create')}
            </Button>
          }
        />
        <div className="admin-integrations-fill">
          <DataTable<SubsysAppItem>
            rowKey="id"
            columns={columns}
            dataSource={items}
            loading={loading}
            scroll={{ x: 1460, y: accessAppsTableScrollY }}
          />
        </div>
      </div>

      <Modal
        open={modalOpen}
        title={t(
          editing ? 'adminAccessApps.editTitle' : 'adminAccessApps.createTitle',
        )}
        onCancel={() => setModalOpen(false)}
        confirmLoading={submitting}
        destroyOnHidden
        maskClosable={false}
        width={640}
        onOk={() => void handleSubmit()}
        okText={t('adminAccessApps.save')}
        cancelText={t('adminAccessApps.cancel')}
      >
        <Form
          form={form}
          layout="vertical"
          initialValues={{ appType: 'subsystem', scope: defaultScope() }}
        >
          <Form.Item name="appType" label={t('adminAccessApps.formType')}>
            <Radio.Group disabled={!!editing}>
              <Radio value="subsystem">{t('adminAccessApps.typeSubsystem')}</Radio>
              <Radio value="app">{t('adminAccessApps.typeApp')}</Radio>
            </Radio.Group>
          </Form.Item>
          {formAppType === 'app' && (
            <Alert
              type="info"
              showIcon
              message={t('adminAccessApps.formTypeAppHint')}
              style={{ marginBottom: spacing.md }}
            />
          )}
          {isSubsystemForm && (
            <Form.Item
              name="key"
              label={t('adminAccessApps.formKey')}
              rules={[
                { required: true, message: t('adminAccessApps.formKeyRequired') },
                {
                  pattern: /^[a-zA-Z][a-zA-Z0-9_-]{1,39}$/,
                  message: t('adminAccessApps.formKeyHint'),
                },
              ]}
              extra={t('adminAccessApps.formKeyHint')}
            >
              <Input
                placeholder={t('adminAccessApps.formKeyPlaceholder')}
                maxLength={40}
                disabled={!!editing}
              />
            </Form.Item>
          )}
          <Form.Item
            name="name"
            label={t('adminAccessApps.formName')}
            rules={[
              {
                required: true,
                message: t('adminAccessApps.formNameRequired'),
              },
              { max: 50, message: t('adminAccessApps.formNameMax') },
            ]}
          >
            <Input
              placeholder={t('adminAccessApps.formNamePlaceholder')}
              maxLength={50}
            />
          </Form.Item>
          <Form.Item
            name="description"
            label={t('adminAccessApps.formDescription')}
            rules={[{ max: 255 }]}
          >
            <Input.TextArea
              placeholder={t('adminAccessApps.formDescriptionPlaceholder')}
              maxLength={255}
              rows={3}
            />
          </Form.Item>
          {isSubsystemForm && (
            <>
              <Form.Item
                label={t('adminAccessApps.formCallbackUrl')}
                extra={t('adminAccessApps.formCallbackUrlHint')}
              >
                <Form.List name="callbackUrls">
                  {(fields, { add, remove }) => (
                    <div style={{ display: 'flex', flexDirection: 'column', gap: spacing.xs }}>
                      {fields.map((field) => (
                        <div key={field.key} style={{ display: 'flex', alignItems: 'flex-start', gap: spacing.xs }}>
                          <Form.Item
                            name={field.name}
                            rules={[
                              { required: true, message: t('adminAccessApps.formCallbackUrlRequired') },
                              { max: 255, message: t('adminAccessApps.formCallbackUrlMax') },
                              {
                                pattern: /^https?:\/\/.+/,
                                message: t('adminAccessApps.formCallbackUrlPattern'),
                              },
                            ]}
                            style={{ flex: 1, marginBottom: 0 }}
                          >
                            <Input
                              placeholder={t('adminAccessApps.formCallbackUrlPlaceholder')}
                              maxLength={255}
                            />
                          </Form.Item>
                          <Button
                            type="text"
                            size="small"
                            danger
                            icon={<DeleteOutlined />}
                            aria-label={t('adminAccessApps.formCallbackUrlRemove')}
                            disabled={fields.length <= 1}
                            onClick={() => remove(field.name)}
                          />
                        </div>
                      ))}
                      <Button
                        type="dashed"
                        size="small"
                        icon={<PlusOutlined />}
                        disabled={fields.length >= 10}
                        onClick={() => add('')}
                      >
                        {t('adminAccessApps.formCallbackUrlAdd')}
                      </Button>
                    </div>
                  )}
                </Form.List>
              </Form.Item>
              <Form.Item
                name="scope"
                label={t('grantScope.title')}
                rules={[
                  {
                    validator: (_, scope: GrantScopeValue | undefined) => {
                      if (!scope || scope.allUsers) return Promise.resolve();
                      const list = scope.orgGrants ?? [];
                      if (list.some((row) => !row.orgRoleId || row.orgRoleId <= 0)) {
                        return Promise.reject(
                          new Error(t('adminAccessApps.formOrgGrantRoleRequired')),
                        );
                      }
                      const roleIds = list.map((row) => row.orgRoleId);
                      if (new Set(roleIds).size !== roleIds.length) {
                        return Promise.reject(
                          new Error(t('adminAccessApps.formOrgGrantsDuplicate')),
                        );
                      }
                      return Promise.resolve();
                    },
                  },
                ]}
              >
                <GrantScopeEditor teamOptions={teamOptions} teamLoading={teamLoading} roleTree={roleTree} />
              </Form.Item>
            </>
          )}
        </Form>
      </Modal>

      <Modal
        title={t('adminAccessApps.revealTitle')}
        open={revealTarget !== null}
        maskClosable={false}
        onCancel={() => setRevealTarget(null)}
        footer={
          <Button type="primary" onClick={() => setRevealTarget(null)}>
            {t('adminAccessApps.credentialSaved')}
          </Button>
        }
        destroyOnHidden
      >
        <Alert
          type="info"
          showIcon
          message={t('adminAccessApps.revealHint')}
          style={{ marginBottom: spacing.md }}
        />
        {revealTarget?.key && (
          <>
            <Text strong>{t('adminAccessApps.appIdLabel')}</Text>
            <Paragraph code copyable style={{ wordBreak: 'break-all' }}>
              {revealTarget.key}
            </Paragraph>
          </>
        )}
        <Text strong>{t('adminAccessApps.secretMaskLabel')}</Text>
        <Paragraph code copyable={false} style={{ wordBreak: 'break-all' }}>
          {revealTarget?.secretMask || '-'}
        </Paragraph>
        {revealedSecret === null ? (
          <Button
            type="primary"
            icon={<EyeOutlined />}
            loading={revealing}
            disabled={!revealTarget?.id}
            onClick={() => revealTarget?.id && void revealById(revealTarget.id)}
          >
            {t('adminAccessApps.revealBtn')}
          </Button>
        ) : (
          <>
            <Text strong>{t('adminAccessApps.secretLabel')}</Text>
            <Paragraph code copyable style={{ marginBottom: 0, wordBreak: 'break-all' }}>
              {revealedSecret}
            </Paragraph>
          </>
        )}
      </Modal>
    </>
  );
}
