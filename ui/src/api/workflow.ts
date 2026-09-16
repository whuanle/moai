import { getApiClient } from '@/api/kiota'

/** 节点执行状态（调试/详情共用，与生成类型 WorkflowNodeExecution 一致） */
export interface WorkflowNodeExecution {
  nodeKey?: string | null
  /** 节点类型：start/end/condition/aiChat/javascript/plugin */
  nodeType?: string | null
  nodeName?: string | null
  /** pending/running/completed/failed/skipped */
  state?: string | null
  /** 解析后的节点输入 JSON 文本，未执行为 null */
  input?: string | null
  /** 节点输出 JSON 文本，未执行为 null */
  output?: string | null
  errorMessage?: string | null
  attempts?: number | null
  startedAt?: string | null
  endedAt?: string | null
}

/** 调试执行响应：一次同步执行的终态快照 */
export interface WorkflowDebugRunResult {
  instanceId?: string | null
  /** created/running/suspended/completed/cancelled */
  status?: string | null
  /** 执行引用的定义版本，调试运行为 0 */
  definitionVersion?: number | null
  /** 工作流最终输出 JSON 文本，未产出为 null */
  output?: string | null
  errorMessage?: string | null
  startedAt?: string | null
  endedAt?: string | null
  nodes?: WorkflowNodeExecution[] | null
}

/** 流程编排配置（草稿/已发布定义、编辑器画布 JSON、版本与状态） */
export interface WorkflowConfig {
  appId?: string | null
  configId?: string | null
  draftDefinition?: string | null
  draftEditorData?: string | null
  publishedDefinition?: string | null
  /** 当前已发布版本号，0=从未发布 */
  version?: number | null
  /** 0=草稿有未发布变更（或从未发布） 1=当前草稿已发布 */
  status?: number | null
  publishTime?: string | null
}

/** 流程运行实例列表项 */
export interface WorkflowInstanceItem {
  instanceId?: string | null
  appId?: string | null
  /** 0=已创建 1=执行中 2=已挂起 3=已完成 4=已取消 */
  status?: number | null
  isDebug?: boolean | null
  /** 执行引用的定义版本号，0=调试执行 */
  version?: number | null
  input?: string | null
  output?: string | null
  errorMessage?: string | null
  startTime?: string | null
  endTime?: string | null
  createTime?: string | null
  createUserName?: string | null
}

/** 流程运行实例详情 */
export interface WorkflowInstanceDetail {
  instanceId?: string | null
  appId?: string | null
  status?: number | null
  isDebug?: boolean | null
  version?: number | null
  input?: string | null
  output?: string | null
  errorMessage?: string | null
  startTime?: string | null
  endTime?: string | null
  nodes?: WorkflowNodeExecution[] | null
}

/** 运行实例分页结果 */
export interface WorkflowInstancesResult {
  items?: WorkflowInstanceItem[] | null
  total?: number | null
  pageNo?: number | null
  pageSize?: number | null
}

/** 查询流程编排配置；未保存过配置时 draftDefinition/draftEditorData 为 null */
export async function getWorkflowConfig(appId: string, teamId: number): Promise<WorkflowConfig> {
  const client = getApiClient()
  const res = await client.api.app.workflow.config.get({
    queryParameters: { appId, teamId: String(teamId) },
  })
  return res ?? {}
}

/** 保存流程编排草稿（流程定义 JSON + 编辑器画布 JSON），需要团队 Admin+ */
export async function saveWorkflowDraft(
  appId: string,
  teamId: number,
  payload: { definition: string; editorData: string },
): Promise<void> {
  const client = getApiClient()
  await client.api.app.workflow.draft.post({
    appId,
    teamId: String(teamId),
    definition: payload.definition,
    editorData: payload.editorData,
  })
}

/** 发布流程编排：校验草稿合法后生成不可变已发布快照（版本递增）并置应用为已发布，需要团队 Admin+ */
export async function publishWorkflow(appId: string, teamId: number): Promise<void> {
  const client = getApiClient()
  await client.api.app.workflow.publish.post({
    appId,
    teamId: String(teamId),
  })
}

/** 调试执行流程（同步到终态，返回节点级执行状态）；可携带最新草稿实现「边改边试」，需要团队 Admin+ */
export async function debugRunWorkflow(
  appId: string,
  teamId: number,
  payload: { inputJson?: string; definition?: string; editorData?: string },
): Promise<WorkflowDebugRunResult> {
  const client = getApiClient()
  const res = await client.api.app.workflow.debugRun.post({
    appId,
    teamId: String(teamId),
    inputJson: payload.inputJson ?? '{}',
    definition: payload.definition,
    editorData: payload.editorData,
  })
  return res ?? {}
}

/** 分页查询流程运行实例列表（需要团队 Admin+） */
export async function getWorkflowInstances(
  appId: string,
  teamId: number,
  params?: { pageNo?: number; pageSize?: number; status?: number },
): Promise<WorkflowInstancesResult> {
  const client = getApiClient()
  const res = await client.api.app.workflow.instances.get({
    queryParameters: {
      appId,
      teamId: String(teamId),
      pageNo: params?.pageNo,
      pageSize: params?.pageSize,
      status: params?.status,
    },
  })
  return res ?? {}
}

/** 查询运行实例详情（含节点级执行状态，需要团队 Admin+） */
export async function getWorkflowInstance(
  appId: string,
  teamId: number,
  instanceId: string,
): Promise<WorkflowInstanceDetail> {
  const client = getApiClient()
  const res = await client.api.app.workflow.instance.get({
    queryParameters: { appId, teamId: String(teamId), instanceId },
  })
  return res ?? {}
}
