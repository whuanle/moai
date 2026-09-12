import {
  createUntypedArray,
  createUntypedBoolean,
  createUntypedNull,
  createUntypedNumber,
  createUntypedObject,
  createUntypedString,
  isUntypedArray,
  isUntypedObject,
  type UntypedNode,
} from '@microsoft/kiota-abstractions'

/**
 * 把普通 JSON 值转换为 Kiota 的 UntypedNode（用于执行参数等自由结构字段）。
 *
 * @param value 任意 JSON 值.
 * @returns UntypedNode.
 */
export function toUntypedNode(value: unknown): UntypedNode {
  if (value === null || value === undefined) return createUntypedNull()
  if (typeof value === 'boolean') return createUntypedBoolean(value)
  if (typeof value === 'number') return createUntypedNumber(value)
  if (typeof value === 'string') return createUntypedString(value)
  if (Array.isArray(value)) return createUntypedArray(value.map((item) => toUntypedNode(item)))
  if (typeof value === 'object') {
    const record: Record<string, UntypedNode> = {}
    for (const [key, item] of Object.entries(value as Record<string, unknown>)) {
      record[key] = toUntypedNode(item)
    }
    return createUntypedObject(record)
  }
  return createUntypedNull()
}

/**
 * 把 Kiota 的 UntypedNode 递归还原为普通 JSON 值。
 *
 * @param node UntypedNode.
 * @returns 普通 JSON 值.
 */
export function fromUntypedNode(node: UntypedNode | null | undefined): unknown {
  if (!node) return undefined
  if (isUntypedObject(node)) {
    const result: Record<string, unknown> = {}
    for (const [key, item] of Object.entries(node.getValue())) {
      result[key] = fromUntypedNode(item)
    }
    return result
  }
  if (isUntypedArray(node)) {
    return node.getValue().map((item) => fromUntypedNode(item))
  }
  return node.getValue()
}
