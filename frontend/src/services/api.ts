import axios from 'axios'
import type { ApiResponse } from '@/types'

const apiClient = axios.create({
  baseURL: '/api',
  timeout: 30000,
  headers: {
    'Content-Type': 'application/json'
  }
})

apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    console.error('API Error:', error)
    return Promise.reject(error)
  }
)

export default apiClient

export const tasksApi = {
  execute: async (taskId: string, inputData: Record<string, any>): Promise<ApiResponse> => {
    const response = await apiClient.post(`/tasks/${taskId}/execute`, inputData)
    return response.data
  },

  stop: async (taskId: string): Promise<ApiResponse> => {
    const response = await apiClient.post(`/tasks/${taskId}/stop`)
    return response.data
  },

  pause: async (taskId: string): Promise<ApiResponse> => {
    const response = await apiClient.post(`/tasks/${taskId}/pause`)
    return response.data
  },

  resume: async (taskId: string): Promise<ApiResponse> => {
    const response = await apiClient.post(`/tasks/${taskId}/resume`)
    return response.data
  },

  getStatus: async (taskId: string): Promise<ApiResponse> => {
    const response = await apiClient.get(`/tasks/${taskId}/status`)
    return response.data
  },

  updateConfig: async (taskId: string, config: Record<string, any>): Promise<ApiResponse> => {
    const response = await apiClient.put(`/tasks/${taskId}/config`, config)
    return response.data
  }
}

export const workflowsApi = {
  start: async (workflowId: string, inputData: Record<string, any>): Promise<ApiResponse> => {
    const response = await apiClient.post(`/workflows/${workflowId}/start`, inputData)
    return response.data
  },

  stop: async (workflowId: string): Promise<ApiResponse> => {
    const response = await apiClient.post(`/workflows/${workflowId}/stop`)
    return response.data
  },

  pause: async (workflowId: string): Promise<ApiResponse> => {
    const response = await apiClient.post(`/workflows/${workflowId}/pause`)
    return response.data
  },

  resume: async (workflowId: string): Promise<ApiResponse> => {
    const response = await apiClient.post(`/workflows/${workflowId}/resume`)
    return response.data
  },

  getStatus: async (workflowId: string): Promise<ApiResponse> => {
    const response = await apiClient.get(`/workflows/${workflowId}/status`)
    return response.data
  },

  addNode: async (workflowId: string, node: any): Promise<ApiResponse> => {
    const response = await apiClient.post(`/workflows/${workflowId}/nodes`, node)
    return response.data
  },

  removeNode: async (workflowId: string, nodeId: string): Promise<ApiResponse> => {
    const response = await apiClient.delete(`/workflows/${workflowId}/nodes/${nodeId}`)
    return response.data
  },

  addConnection: async (workflowId: string, connection: any): Promise<ApiResponse> => {
    const response = await apiClient.post(`/workflows/${workflowId}/connections`, connection)
    return response.data
  },

  removeConnection: async (workflowId: string, connectionId: string): Promise<ApiResponse> => {
    const response = await apiClient.delete(`/workflows/${workflowId}/connections/${connectionId}`)
    return response.data
  },

  skipNode: async (workflowId: string, nodeId: string): Promise<ApiResponse> => {
    const response = await apiClient.post(`/workflows/${workflowId}/nodes/${nodeId}/skip`)
    return response.data
  },

  terminate: async (workflowId: string): Promise<ApiResponse> => {
    const response = await apiClient.post(`/workflows/${workflowId}/terminate`)
    return response.data
  }
}

export const schedulesApi = {
  schedule: async (request: { taskDefinitionId?: string; workflowDefinitionId?: string; cronExpression: string }): Promise<ApiResponse> => {
    const response = await apiClient.post('/schedules', request)
    return response.data
  },

  unschedule: async (scheduleId: string): Promise<ApiResponse> => {
    const response = await apiClient.delete(`/schedules/${scheduleId}`)
    return response.data
  },

  updateSchedule: async (scheduleId: string, cronExpression: string): Promise<ApiResponse> => {
    const response = await apiClient.put(`/schedules/${scheduleId}`, { cronExpression })
    return response.data
  },

  getSchedules: async (): Promise<ApiResponse> => {
    const response = await apiClient.get('/schedules')
    return response.data
  }
}

export const mqttApi = {
  subscribe: async (topic: string): Promise<ApiResponse> => {
    const response = await apiClient.post('/mqtt/subscribe', { topic })
    return response.data
  },

  unsubscribe: async (topic: string): Promise<ApiResponse> => {
    const response = await apiClient.post('/mqtt/unsubscribe', { topic })
    return response.data
  },

  publish: async (topic: string, payload: string): Promise<ApiResponse> => {
    const response = await apiClient.post('/mqtt/publish', { topic, payload })
    return response.data
  }
}
