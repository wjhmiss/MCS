export interface TaskDefinition {
  id: string
  name: string
  description: string
  taskType: string
  config: Record<string, any>
  isEnabled: boolean
  createdAt: string
  updatedAt: string
  createdBy: string
}

export interface WorkflowDefinition {
  id: string
  name: string
  description: string
  version: number
  isEnabled: boolean
  createdAt: string
  updatedAt: string
  createdBy: string
}

export interface WorkflowNode {
  id: string
  workflowId: string
  taskDefinitionId?: string
  name: string
  nodeType: string
  positionX: number
  positionY: number
  config: Record<string, any>
  executionOrder: number
  isConcurrent: boolean
  waitForPrevious: boolean
  skipConditions: string[]
  createdAt: string
  updatedAt: string
}

export interface WorkflowConnection {
  id: string
  workflowId: string
  fromNodeId: string
  toNodeId: string
  conditionExpression: string
  createdAt: string
}

export interface TaskExecution {
  id: string
  taskDefinitionId?: string
  workflowExecutionId?: string
  workflowNodeId?: string
  status: string
  startTime: string
  endTime: string
  inputData: Record<string, any>
  outputData: Record<string, any>
  errorMessage: string
  retryCount: number
  createdAt: string
  updatedAt: string
}

export interface WorkflowExecution {
  id: string
  workflowDefinitionId: string
  status: string
  startTime: string
  endTime: string
  triggeredBy: string
  triggerType: string
  inputData: Record<string, any>
  outputData: Record<string, any>
  errorMessage: string
  createdAt: string
  updatedAt: string
}

export interface Schedule {
  id: string
  taskDefinitionId?: string
  workflowDefinitionId?: string
  cronExpression: string
  isEnabled: boolean
  nextRunTime: string
  lastRunTime: string
  timezone: string
  createdAt: string
  updatedAt: string
}

export interface MQTTConfig {
  id: string
  taskDefinitionId?: string
  topic: string
  qos: number
  retain: boolean
  isPublisher: boolean
  isSubscriber: boolean
  config: Record<string, any>
  createdAt: string
  updatedAt: string
}

export interface APIConfig {
  id: string
  taskDefinitionId?: string
  url: string
  method: string
  headers: Record<string, string>
  bodyTemplate: Record<string, any>
  timeout: number
  retryPolicy: Record<string, any>
  createdAt: string
  updatedAt: string
}

export interface ExternalTrigger {
  id: string
  triggerType: string
  source: string
  payload: Record<string, any>
  targetTaskDefinitionId?: string
  targetWorkflowDefinitionId?: string
  status: string
  errorMessage: string
  createdAt: string
}

export interface Alert {
  id: string
  alertType: string
  severity: string
  title: string
  message: string
  relatedExecutionId?: string
  relatedTaskId?: string
  isResolved: boolean
  resolvedAt: string
  createdAt: string
}

export interface SystemLog {
  id: string
  logLevel: string
  message: string
  source: string
  additionalData: Record<string, any>
  createdAt: string
}

export interface ApiResponse<T = any> {
  success: boolean
  result?: string
  data?: T
  error?: string
}
