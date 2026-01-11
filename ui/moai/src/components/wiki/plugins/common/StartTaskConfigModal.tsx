import { useState, useEffect } from "react";
import {
  Modal,
  Form,
  Switch,
  Radio,
  InputNumber,
  Input,
  Select,
  Checkbox,
  Alert,
  Row,
  Col,
  Divider,
} from "antd";
import type {
  WikiPluginAutoProcessConfig,
  WikiDocumentTextPartitionPreviewCommand,
  WikiDocumentAiTextPartionCommand,
} from "../../../../apiClient/models";
import { useAiModelList } from "../../wiki_hooks";

const { TextArea } = Input;

/**
 * 预处理策略选项
 */
const PreprocessStrategyOptions = [
  { label: "大纲生成", value: "outlineGeneration" },
  { label: "问题生成", value: "questionGeneration" },
  { label: "关键词摘要融合", value: "keywordSummaryFusion" },
  { label: "语义聚合", value: "semanticAggregation" },
];

interface StartTaskConfigModalProps {
  open: boolean;
  onCancel: () => void;
  onConfirm: (
    isAutoProcess: boolean,
    autoProcessConfig: WikiPluginAutoProcessConfig | null
  ) => void;
  wikiId: number;
}

/**
 * 启动任务配置模态窗口
 * 用于配置 IsAutoProcess 和 AutoProcessConfig
 */
export default function StartTaskConfigModal({
  open,
  onCancel,
  onConfirm,
  wikiId,
}: StartTaskConfigModalProps) {
  const [form] = Form.useForm();
  const [partitionType, setPartitionType] = useState<"normal" | "ai">("normal");
  const [isEmbedding, setIsEmbedding] = useState(false);
  const [hasPreprocessStrategy, setHasPreprocessStrategy] = useState(false);
  
  // 使用共享 hook 获取 AI 模型列表
  const { modelList, loading: modelListLoading, contextHolder, fetchModelList } = useAiModelList(wikiId, "chat");

  // 获取 AI 模型列表
  useEffect(() => {
    if (open) {
      fetchModelList();
    }
  }, [open, fetchModelList]);

  // 重置表单
  const handleCancel = () => {
    form.resetFields();
    setPartitionType("normal");
    setIsEmbedding(false);
    setHasPreprocessStrategy(false);
    onCancel();
  };

  // 提交配置
  const handleConfirm = async () => {
    try {
      // 验证相应的字段
      const fieldsToValidate: string[] = [];
      if (partitionType === "normal") {
        fieldsToValidate.push("maxTokensPerChunk", "overlap");
      } else {
        fieldsToValidate.push("aiModelId");
      }
      // 如果选择了文档处理策略，则必须选择策略化AI模型
      const values = form.getFieldsValue();
      if (values.preprocessStrategyType && values.preprocessStrategyType.length > 0) {
        fieldsToValidate.push("preprocessStrategyAiModel");
      }

      // 只验证需要验证的字段
      if (fieldsToValidate.length > 0) {
        await form.validateFields(fieldsToValidate);
      }

      let autoProcessConfig: WikiPluginAutoProcessConfig | null = null;

      let partion: WikiDocumentTextPartitionPreviewCommand | null = null;
      let aiPartion: WikiDocumentAiTextPartionCommand | null = null;

      // 根据切割类型设置配置
      if (partitionType === "normal") {
        partion = {
          maxTokensPerChunk: values.maxTokensPerChunk || 1000,
          overlap: values.overlap || 100,
          chunkHeader: values.chunkHeader || null,
        };
      } else {
        aiPartion = {
          aiModelId: values.aiModelId,
          promptTemplate: values.promptTemplate || null,
        };
      }

      autoProcessConfig = {
        partion: partion,
        aiPartion: aiPartion,
        preprocessStrategyType: values.preprocessStrategyType || null,
        preprocessStrategyAiModel: values.preprocessStrategyAiModel || null,
        isEmbedding: isEmbedding || false,
        isEmbedSourceText: isEmbedding ? (values.isEmbedSourceText || false) : false,
        threadCount: isEmbedding ? (values.threadCount || null) : null,
      };

      onConfirm(true, autoProcessConfig);
      handleCancel();
    } catch (error) {
      console.error("Form validation failed:", error);
    }
  };

  return (
    <>
      {contextHolder}
      <Modal
        title="启动任务配置"
        open={open}
        onCancel={handleCancel}
        onOk={handleConfirm}
        width={800}
        destroyOnClose={false}
        maskClosable={ false}
      >
        <Form form={form} layout="vertical">
          <Divider orientation="left">切割配置</Divider>

          {/* 切割方式选择 */}
          <Form.Item
            label="切割方式"
            tooltip="选择普通切割或 AI 智能切割"
          >
            <Radio.Group
              value={partitionType}
              onChange={(e) => {
                setPartitionType(e.target.value);
                // 切换切割类型时，清除另一个类型的字段值
                if (e.target.value === "normal") {
                  form.setFieldsValue({
                    aiModelId: undefined,
                    promptTemplate: undefined,
                  });
                } else {
                  form.setFieldsValue({
                    maxTokensPerChunk: undefined,
                    overlap: undefined,
                    chunkHeader: undefined,
                  });
                }
              }}
            >
              <Radio value="normal">普通切割</Radio>
              <Radio value="ai">AI 切割</Radio>
            </Radio.Group>
          </Form.Item>

          {/* 普通切割配置 */}
          {partitionType === "normal" && (
            <>
              <Row gutter={16}>
                <Col span={12}>
                  <Form.Item
                    label="每段最大Token数"
                    name="maxTokensPerChunk"
                    rules={[
                      { required: true, message: "请输入每段最大Token数" },
                    ]}
                    initialValue={1000}
                  >
                    <InputNumber
                      min={1}
                      max={100000}
                      style={{ width: "100%" }}
                      placeholder="默认 1000"
                    />
                  </Form.Item>
                </Col>
                <Col span={12}>
                  <Form.Item
                    label="重叠Token数"
                    name="overlap"
                    rules={[{ required: true, message: "请输入重叠Token数" }]}
                    initialValue={100}
                  >
                    <InputNumber
                      min={0}
                      max={1000}
                      style={{ width: "100%" }}
                      placeholder="默认 100"
                    />
                  </Form.Item>
                </Col>
              </Row>
              <Form.Item
                label="分块标题（可选）"
                name="chunkHeader"
                tooltip="可选，在每个分块前添加的标题"
              >
                <Input placeholder="请输入分块标题（可选）" />
              </Form.Item>
            </>
          )}

          {/* AI 切割配置 */}
          {partitionType === "ai" && (
            <>
              <Form.Item
                label="AI模型"
                name="aiModelId"
                rules={[{ required: true, message: "请选择AI模型" }]}
              >
                <Select
                  placeholder="请选择AI模型"
                  loading={modelListLoading}
                  options={modelList.map((model) => ({
                    label: model.name,
                    value: model.id,
                  }))}
                />
              </Form.Item>
              <Form.Item
                label="提示词模板"
                name="promptTemplate"
                tooltip="用于指导AI进行智能切割的提示词模板，请务必提示模型输出 JSON 格式的数据"
                initialValue={`你是一个专业的中文知识库文档拆分助手。

请根据用户提供的完整文档内容按照以下要求拆分文本：

1. 每个文本块长度尽量不超过 1000 个字符，尽可能不要切开多个文本块，可根据语义适当调整，如果长度够用，则请勿拆分多个块。
2. 在有多个文本块的情况下，则相邻文本块需要保留约 50 个字符的重叠内容以保证上下文衔接，只有一个块则不需要生成重叠内容。
3.尽可能不要拆开代码或段落，尽可能让语义相近的内容在一个段落内。

3. 只允许引用原文内容，不要编造或总结。

4. 输出统一使用 JSON 字符串数组，格式如下：

[
"第一块原文内容",
"第二块原文内容"
]

只输出 JSON，不要附加其他解释。`}
              >
                <TextArea
                  rows={8}
                  placeholder="请输入提示词模板"
                />
              </Form.Item>
            </>
          )}

          <Divider orientation="left">元数据生成配置</Divider>

          {/* 文档处理策略 */}
          <Form.Item
            label="文档处理策略"
            name="preprocessStrategyType"
            tooltip="使用何种文档处理策略生成元数据，如果不填写则不生成"
          >
            <Select
              mode="multiple"
              placeholder="请选择文档处理策略（可选）"
              options={PreprocessStrategyOptions}
              allowClear
              onChange={(value) => {
                setHasPreprocessStrategy(value && value.length > 0);
                // 如果清空了策略，也清空AI模型选择
                if (!value || value.length === 0) {
                  form.setFieldsValue({ preprocessStrategyAiModel: undefined });
                }
              }}
            />
          </Form.Item>

          {/* 策略化AI模型 */}
          <Form.Item
            label="策略化AI模型"
            name="preprocessStrategyAiModel"
            tooltip={hasPreprocessStrategy ? "启用文档处理策略时，必须选择策略化AI模型" : "策略化AI模型，如果不填写则不使用"}
            rules={hasPreprocessStrategy ? [
              { required: true, message: "启用文档处理策略时，必须选择策略化AI模型" }
            ] : []}
          >
            <Select
              placeholder={hasPreprocessStrategy ? "请选择策略化AI模型（必填）" : "请选择策略化AI模型（可选）"}
              loading={modelListLoading}
              options={modelList.map((model) => ({
                label: model.name,
                value: model.id,
              }))}
              allowClear
            />
          </Form.Item>

          <Divider orientation="left">向量化配置</Divider>

          {/* 是否向量化 */}
          <Form.Item
            label="是否自动向量化"
            tooltip="如果不启用，则不需要填写向量化相关配置"
          >
            <Switch
              checked={isEmbedding}
              onChange={setIsEmbedding}
              checkedChildren="启用"
              unCheckedChildren="禁用"
            />
          </Form.Item>

          {/* 向量化详细配置 */}
          {isEmbedding && (
            <>
              <Form.Item
                label="是否将 chunk 源文本也向量化"
                name="isEmbedSourceText"
                valuePropName="checked"
                initialValue={false}
              >
                <Checkbox>将 chunk 源文本也向量化</Checkbox>
              </Form.Item>
              <Form.Item
                label="并发线程数量"
                name="threadCount"
                tooltip="并发线程数量，用于控制向量化任务的并发度（1-100）"
                initialValue={5}
              >
                <InputNumber
                  min={1}
                  max={100}
                  style={{ width: "100%" }}
                  placeholder="默认 5"
                />
              </Form.Item>
            </>
          )}

          <Alert
            message="配置说明"
            description="普通切割适合对称检索，按照固定的token数量和重叠数量对文档进行分段。AI 切割使用AI模型来理解文档内容，按照语义和上下文进行更智能的分段。"
            type="info"
            showIcon
            style={{ marginTop: 16 }}
          />
        </Form>
      </Modal>
    </>
  );
}

