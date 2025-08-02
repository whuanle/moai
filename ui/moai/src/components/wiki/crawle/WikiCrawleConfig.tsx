import { useState, useEffect } from "react";
import { useParams } from "react-router";
import {
  Card,
  Form,
  Input,
  Button,
  message,
  Switch,
  InputNumber,
  Space,
  Spin,
  Typography,
  Alert,
  Row,
  Col,
  Modal,
  Select,
} from "antd";
import { PlayCircleOutlined } from "@ant-design/icons";
import { GetApiClient } from "../../ServiceClient";
import {
  QueryWikiConfigInfoCommandResponse,
  UpdateWebDocumentConfigCommand,
  StartWebDocumentCrawleCommand,
  EmbeddingTokenizer,
} from "../../../apiClient/models";
import { proxyRequestError } from "../../../helper/RequestError";

const { Title, Text } = Typography;

interface CrawleConfigFormData {
  title: string;
  address: string;
  limitAddress: string;
  limitMaxCount: number;
  isAutoEmbedding: boolean;
  isCrawlOther: boolean;
  isWaitJs: boolean;
  selector: string;
}

interface CrawleParametersFormData {
  splitMethod: string;
  maxTokensPerParagraph: number;
  overlappingTokens: number;
  tokenizer: EmbeddingTokenizer;
}

export default function WikiCrawleConfig() {
  const { id: wikiId, crawleId: wikiWebConfigId } = useParams();
  const [form] = Form.useForm();
  const [parametersForm] = Form.useForm();
  const [messageApi, contextHolder] = message.useMessage();
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [startingCrawle, setStartingCrawle] = useState(false);
  const [configData, setConfigData] =
    useState<QueryWikiConfigInfoCommandResponse | null>(null);
  const [parametersModalVisible, setParametersModalVisible] = useState(false);

  const apiClient = GetApiClient();

  useEffect(() => {
    if (wikiId && wikiWebConfigId) {
      fetchCrawleConfig();
    }
  }, [wikiId, wikiWebConfigId]);

  const fetchCrawleConfig = async () => {
    try {
      setLoading(true);
      const response = await apiClient.api.wiki.web.query_crawle_config.get({
        queryParameters: {
          wikiId: parseInt(wikiId || "0"),
          wikiWebConfigId: parseInt(wikiWebConfigId || "0"),
        },
      });

      if (response) {
        setConfigData(response);
        // 设置表单初始值
        form.setFieldsValue({
          title: response.title || "",
          address: response.address || "",
          limitAddress: response.limitAddress || "",
          limitMaxCount: response.limitMaxCount || 100,
          isAutoEmbedding: response.isAutoEmbedding || false,
          isCrawlOther: response.isCrawlOther || false,
          isWaitJs: response.isWaitJs || false,
          selector: response.selector || "",
        });
      }
    } catch (error) {
      console.error("Failed to fetch crawle config:", error);
      messageApi.error("获取爬虫配置失败");
    } finally {
      setLoading(false);
    }
  };

  const handleSubmit = async (values: CrawleConfigFormData) => {
    try {
      setSaving(true);

      const updateCommand: UpdateWebDocumentConfigCommand = {
        webConfigId: parseInt(wikiWebConfigId || "0"),
        wikiId: parseInt(wikiId || "0"),
        title: values.title,
        address: values.address,
        limitAddress: values.limitAddress,
        limitMaxCount: values.limitMaxCount,
        isAutoEmbedding: values.isAutoEmbedding,
        isCrawlOther: values.isCrawlOther,
        isWaitJs: values.isWaitJs,
        selector: values.selector,
      };

      await apiClient.api.wiki.web.update_crawle_config.post(updateCommand);

      messageApi.success("配置更新成功");
      // 重新获取配置数据
      await fetchCrawleConfig();
    } catch (error) {
      console.error("Failed to update crawle config:", error);
      messageApi.error("配置更新失败");
    } finally {
      setSaving(false);
    }
  };

  const handleStartCrawle = () => {
    // 显示参数配置模态窗口
    setParametersModalVisible(true);
    // 设置参数表单默认值
    parametersForm.setFieldsValue({
      splitMethod: "",
      maxTokensPerParagraph: 1000,
      overlappingTokens: 100,
      tokenizer: "cl100k",
    });
  };

  const handleParametersSubmit = async (values: CrawleParametersFormData) => {
    try {
      setStartingCrawle(true);
      setParametersModalVisible(false);

      const formValues = form.getFieldsValue();
      const startCommand: StartWebDocumentCrawleCommand = {
        webConfigId: parseInt(wikiWebConfigId || "0"),
        wikiId: parseInt(wikiId || "0"),
        splitMethod: values.splitMethod || "",
        maxTokensPerParagraph: values.maxTokensPerParagraph,
        overlappingTokens: values.overlappingTokens,
        tokenizer: values.tokenizer,
      };

      const response = await apiClient.api.wiki.web.lanuch_crawle.post(startCommand);

      if (response) {
        messageApi.success("爬虫启动成功");
      }
    } catch (error) {
      console.error("Failed to start crawle:", error);
      proxyRequestError(error, messageApi, "爬虫启动失败");
    } finally {
      setStartingCrawle(false);
    }
  };

  const handleParametersCancel = () => {
    setParametersModalVisible(false);
    parametersForm.resetFields();
  };

  const handleReset = () => {
    if (configData) {
      form.setFieldsValue({
        title: configData.title || "",
        address: configData.address || "",
        limitAddress: configData.limitAddress || "",
        limitMaxCount: configData.limitMaxCount || 100,
        isAutoEmbedding: configData.isAutoEmbedding || false,
        isCrawlOther: configData.isCrawlOther || false,
        isWaitJs: configData.isWaitJs || false,
        selector: configData.selector || "",
      });
    }
  };

  if (loading) {
    return (
      <div style={{ textAlign: "center", padding: "50px" }}>
        <Spin size="large" />
        <div style={{ marginTop: 16 }}>
          <Text>加载配置中...</Text>
        </div>
      </div>
    );
  }

  return (
    <>
      {contextHolder}
      <Card>
        <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
          <Col flex="auto">
            <div>
              <Title level={3}>爬虫配置</Title>
              <Text type="secondary">
                配置网页爬虫的相关参数，包括目标地址、限制条件等
              </Text>
            </div>
          </Col>
          <Col>
            <Button
              type="primary"
              icon={<PlayCircleOutlined />}
              loading={startingCrawle}
              onClick={handleStartCrawle}
              disabled={!configData}
            >
              启动爬虫
            </Button>
          </Col>
        </Row>

        {configData && (
          <Alert
            message="配置信息"
            description={`配置ID: ${configData.wikiConfigId} | 知识库ID: ${configData.wikiId}`}
            type="info"
            showIcon
            style={{ marginBottom: 24 }}
          />
        )}

        <Form
          form={form}
          layout="vertical"
          onFinish={handleSubmit}
          initialValues={{
            title: "",
            address: "",
            limitAddress: "",
            limitMaxCount: 100,
            isAutoEmbedding: false,
            isCrawlOther: false,
            isWaitJs: false,
            selector: "",
          }}
        >
          <Form.Item
            label="配置名称"
            name="title"
            rules={[{ required: true, message: "请输入配置名称" }]}
          >
            <Input placeholder="请输入配置名称" />
          </Form.Item>

          <Form.Item
            label="目标地址"
            name="address"
            rules={[
              { required: true, message: "请输入目标地址" },
              { type: "url", message: "请输入有效的URL地址" },
            ]}
          >
            <Input placeholder="请输入要爬取的网页地址" />
          </Form.Item>

          <Form.Item
            label="限制地址"
            name="limitAddress"
            extra="限制自动爬取的网页都在该路径之下，必须与页面地址具有相同域名"
          >
            <Input placeholder="可选：限制爬取范围" />
          </Form.Item>

          <Form.Item
            label="页面筛选规则"
            name="selector"
            extra="CSS选择器规则，用于提取页面中的特定内容。例如：.content, #main, article 等。留空则提取整个页面内容"
          >
            <Input placeholder="可选：CSS选择器，如 .content, #main, article" />
          </Form.Item>

          <Form.Item
            label="最大爬取数量"
            name="limitMaxCount"
            rules={[{ required: true, message: "请输入最大爬取数量" }]}
          >
            <InputNumber
              min={1}
              max={10000}
              placeholder="请输入最大爬取数量"
              style={{ width: "100%" }}
            />
          </Form.Item>

          <Form.Item
            label="自动向量化"
            name="isAutoEmbedding"
            valuePropName="checked"
            extra="爬取完成后自动进行文档向量化处理"
          >
            <Switch />
          </Form.Item>

          <Form.Item
            label="爬取其他链接"
            name="isCrawlOther"
            valuePropName="checked"
            extra="开启后会自动爬取页面中的链接，加入到待爬取列表中"
          >
            <Switch />
          </Form.Item>

          <Form.Item
            label="等待JavaScript执行"
            name="isWaitJs"
            valuePropName="checked"
            extra="开启此功能不一定可以抓取到完整内容，只有在抓取效果不满意时尝试开启"
          >
            <Switch />
          </Form.Item>

          <Form.Item>
            <Space>
              <Button type="primary" htmlType="submit" loading={saving}>
                保存配置
              </Button>
              <Button onClick={handleReset}>重置</Button>
            </Space>
          </Form.Item>
        </Form>
      </Card>

      {/* 启动爬虫参数配置模态窗口 */}
      <Modal
        title="配置爬虫参数"
        open={parametersModalVisible}
        onCancel={handleParametersCancel}
        footer={null}
        width={600}
        destroyOnClose
      >
        <Form
          form={parametersForm}
          layout="vertical"
          onFinish={handleParametersSubmit}
          initialValues={{
            splitMethod: "",
            maxTokensPerParagraph: 1000,
            overlappingTokens: 100,
            tokenizer: "cl100k",
          }}
        >
          <Row gutter={[16, 16]}>
            <Col span={12}>
              <div>
                <div
                  style={{
                    fontSize: "14px",
                    fontWeight: 500,
                    marginBottom: "8px",
                  }}
                >
                  分词器
                </div>
                <div
                  style={{
                    fontSize: "12px",
                    marginBottom: "8px",
                    color: "#8c8c8c",
                  }}
                >
                  本地检测文档token数量的算法
                </div>
                <Form.Item
                  name="tokenizer"
                  rules={[{ required: true, message: "请选择分词器" }]}
                >
                  <Select
                    placeholder="请选择分词器"
                    options={[
                      { label: "p50k", value: "p50k" },
                      { label: "cl100k", value: "cl100k" },
                      { label: "o200k", value: "o200k" },
                    ]}
                  />
                </Form.Item>
              </div>
            </Col>
            <Col span={12}>
              <div>
                <div
                  style={{
                    fontSize: "14px",
                    fontWeight: 500,
                    marginBottom: "8px",
                  }}
                >
                  每段最大Token数
                </div>
                <div
                  style={{
                    fontSize: "12px",
                    marginBottom: "8px",
                    color: "#8c8c8c",
                  }}
                >
                  当对文档进行分段时，每个分段通常包含一个段落。此参数控制每个段落的最大token数量
                </div>
                <Form.Item
                  name="maxTokensPerParagraph"
                  rules={[
                    { required: true, message: "请输入每段最大Token数" },
                  ]}
                >
                  <InputNumber
                    min={1}
                    max={4000}
                    style={{ width: "100%" }}
                    placeholder="请输入每段最大Token数"
                  />
                </Form.Item>
              </div>
            </Col>
          </Row>

          <Row gutter={[16, 16]}>
            <Col span={12}>
              <div>
                <div
                  style={{
                    fontSize: "14px",
                    fontWeight: 500,
                    marginBottom: "8px",
                  }}
                >
                  重叠Token数
                </div>
                <div
                  style={{
                    fontSize: "12px",
                    marginBottom: "8px",
                    color: "#8c8c8c",
                  }}
                >
                  分段之间的重叠token数量，用于保持上下文的连贯性
                </div>
                <Form.Item
                  name="overlappingTokens"
                  rules={[{ required: true, message: "请输入重叠Token数" }]}
                >
                  <InputNumber
                    min={0}
                    max={1000}
                    style={{ width: "100%" }}
                    placeholder="请输入重叠Token数"
                  />
                </Form.Item>
              </div>
            </Col>
            <Col span={12}>
              <div>
                <div
                  style={{
                    fontSize: "14px",
                    fontWeight: 500,
                    marginBottom: "8px",
                  }}
                >
                  文本分割方法
                </div>
                <div
                  style={{
                    fontSize: "12px",
                    marginBottom: "8px",
                    color: "#8c8c8c",
                  }}
                >
                  文本分割方法，暂时不支持使用
                </div>
                <Form.Item name="splitMethod">
                  <Input
                    placeholder="可选：文本分割方法"
                    disabled
                  />
                </Form.Item>
              </div>
            </Col>
          </Row>

          <Form.Item style={{ marginTop: 24, marginBottom: 0 }}>
            <Space style={{ width: "100%", justifyContent: "flex-end" }}>
              <Button onClick={handleParametersCancel}>
                取消
              </Button>
              <Button
                type="primary"
                htmlType="submit"
                loading={startingCrawle}
                icon={<PlayCircleOutlined />}
              >
                启动爬虫
              </Button>
            </Space>
          </Form.Item>
        </Form>

        <Alert
          message="参数说明"
          description="这些参数用于控制爬取内容的文本处理和向量化过程，将影响后续的搜索和匹配效果。"
          type="info"
          showIcon
          style={{ marginTop: 16 }}
        />
      </Modal>
    </>
  );
}
