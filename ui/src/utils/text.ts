/**
 * 把「1. xxx；2. xxx；3. xxx」形态的指引文案拆成步骤数组（中英文均以「N. 」序号分段）。
 * 去掉序号前缀与段尾分号，由调用方用有序列表重新编号展示。
 */
export function splitNumberedSteps(text: string): string[] {
  return text
    .split(/(?=\d+\.\s)/)
    .map((step) => step.replace(/^\d+\.\s*/, '').replace(/[；;]\s*$/, '').trim())
    .filter(Boolean)
}
